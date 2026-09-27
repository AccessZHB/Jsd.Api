using System.Text.Json;
using AutoMapper;
using FluentValidation;
using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Task;
using Jsd.Api.Repositories;
using Jsd.Api.Validators;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Jsd.Api.Services;

/// <summary>
/// 定时任务服务实现（自动任务模块）
///
/// ⚠️ 关键约定（与文档避坑项一一对应，改动前请先读）：
/// 1) Cron 双重校验：保存前 new CronExpression(cron) 抛异常即拒绝，不让非法表达式进调度器。
/// 2) 反射类存在性：job_type 必须是本程序集内存在的 IJob 实现类，否则拒绝新增/修改/启动。
/// 3) 删除顺序：先 scheduler.DeleteJob(JobKey) 再删 sys_job 记录，否则 Quartz 残留脏数据。
/// 4) 更新任务：先 UnscheduleJob 摘旧 Trigger 再重建（Quartz 不支持直接改 Trigger 的 Cron）。
/// 5) 并发控制：concurrent=0 时 JobBuilder.DisallowConcurrentExecution()。
/// 6) run/{id} 幂等：任务不存在或没调度到调度器时直接拒绝，不重复触发。
/// </summary>
public class JobService : IJobService
{
    private const string RetryLeftKey = "retryLeft";
    private const string RetryIntervalKey = "retryInterval";

    private readonly ISysJobRepository _jobRepository;
    private readonly ISysJobLogRepository _logRepository;
    private readonly IScheduler _scheduler;
    private readonly CurrentUserService _currentUser;
    private readonly IMapper _mapper;
    private readonly ILogger<JobService> _logger;

    public JobService(
        ISysJobRepository jobRepository,
        ISysJobLogRepository logRepository,
        IScheduler scheduler,
        CurrentUserService currentUser,
        IMapper mapper,
        ILogger<JobService> logger)
    {
        _jobRepository = jobRepository;
        _logRepository = logRepository;
        _scheduler = scheduler;
        _currentUser = currentUser;
        _mapper = mapper;
        _logger = logger;
    }

    // ==================================================================
    // 一、查询
    // ==================================================================

    /// <inheritdoc/>
    public async Task<ApiResponse<PagedResult<JobDto>>> GetPagedAsync(JobQueryDto query)
    {
        if (query.Page < 1) query.Page = 1;
        if (query.PageSize < 1 || query.PageSize > 200) query.PageSize = 10;

        var (items, total) = await _jobRepository.GetPagedAsync(query);

        var list = _mapper.Map<List<JobDto>>(items);

        // 补 Quartz 侧的下次触发时间（批量并发取，避免串行 N 次数据库往返）
        var jobs = await Task.WhenAll(list.Select(async dto =>
        {
            var trigger = await _scheduler.GetTriggersOfJob(JobKeyOf(dto.JobName, dto.JobGroup));
            var next = trigger
                .Select(t => t.GetNextFireTimeUtc()?.ToLocalTime().DateTime)
                .Where(x => x.HasValue)
                .OrderBy(x => x)
                .FirstOrDefault();
            dto.NextValidTime = next == default ? null : next;
            return dto;
        }));

        return ApiResponse<PagedResult<JobDto>>.Success(
            PagedResult<JobDto>.Create(jobs.ToList(), total, query.Page, query.PageSize),
            "查询成功");
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<JobDto?>> GetByIdAsync(long id)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job is null) return ApiResponse<JobDto?>.Fail("任务不存在");

        var dto = _mapper.Map<JobDto>(job);
        dto.StatusText = JobTexts.JobStatus(job.Status);
        dto.MisfirePolicyText = JobTexts.MisfirePolicy(job.MisfirePolicy);
        dto.ConcurrentText = JobTexts.Concurrent(job.Concurrent);

        var triggers = await _scheduler.GetTriggersOfJob(JobKeyOf(job.JobName, job.JobGroup));
        var next = triggers
            .Select(t => t.GetNextFireTimeUtc()?.ToLocalTime().DateTime)
            .Where(x => x.HasValue)
            .OrderBy(x => x)
            .FirstOrDefault();
        dto.NextValidTime = next == default ? null : next;

        return ApiResponse<JobDto?>.Success(dto);
    }

    // ==================================================================
    // 二、增删改
    // ==================================================================

    /// <inheritdoc/>
    public async Task<ApiResponse<long>> CreateAsync(CreateJobDto dto)
    {
        var validator = new CreateJobValidator();
        validator.ValidateAndThrow(dto);

        var jobName = dto.JobName.Trim();
        var jobGroup = string.IsNullOrWhiteSpace(dto.JobGroup) ? "DEFAULT" : dto.JobGroup.Trim();
        var cron = dto.CronExpression.Trim();
        var jobType = dto.JobType.Trim();

        EnsureCronValid(cron);
        EnsureJobTypeValid(jobType);

        if (await _jobRepository.ExistsNameAsync(jobName, jobGroup, 0))
        {
            return ApiResponse<long>.Fail($"分组「{jobGroup}」下已存在名为「{jobName}」的任务");
        }

        var entity = new SysJob
        {
            JobName = jobName,
            JobGroup = jobGroup,
            JobType = jobType,
            CronExpression = cron,
            JobParams = string.IsNullOrWhiteSpace(dto.JobParams) ? null : dto.JobParams.Trim(),
            Status = dto.Status,
            MisfirePolicy = dto.MisfirePolicy,
            Concurrent = dto.Concurrent,
            Timeout = dto.Timeout,
            RetryCount = dto.RetryCount,
            RetryInterval = dto.RetryInterval,
            Remark = string.IsNullOrWhiteSpace(dto.Remark) ? null : dto.Remark.Trim(),
            CreateBy = _currentUser.UserName,
            UpdateBy = _currentUser.UserName,
        };

        await _jobRepository.AddAsync(entity);
        await _jobRepository.SaveChangesAsync();

        // 新增即运行：立刻登记到 Quartz；失败不影响任务已入库，留给下个周期
        if (entity.Status == 1)
        {
            try
            {
                await ScheduleJobAsync(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "新增任务后调度到 Quartz 失败（jobId={JobId}）", entity.Id);
            }
        }

        return ApiResponse<long>.Success(entity.Id, "新增成功");
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<bool>> UpdateAsync(UpdateJobDto dto)
    {
        var validator = new UpdateJobValidator();
        validator.ValidateAndThrow(dto);

        var jobName = dto.JobName.Trim();
        var jobGroup = string.IsNullOrWhiteSpace(dto.JobGroup) ? "DEFAULT" : dto.JobGroup.Trim();
        var cron = dto.CronExpression.Trim();
        var jobType = dto.JobType.Trim();

        EnsureCronValid(cron);
        EnsureJobTypeValid(jobType);

        var entity = await _jobRepository.GetByIdAsync(dto.Id);
        if (entity is null) return ApiResponse<bool>.Fail("任务不存在");

        if (await _jobRepository.ExistsNameAsync(jobName, jobGroup, dto.Id))
        {
            return ApiResponse<bool>.Fail($"分组「{jobGroup}」下已存在名为「{jobName}」的任务");
        }

        var nameChanged = !string.Equals(entity.JobName, jobName, StringComparison.Ordinal)
                          || !string.Equals(entity.JobGroup, jobGroup, StringComparison.Ordinal);

        entity.JobName = jobName;
        entity.JobGroup = jobGroup;
        entity.JobType = jobType;
        entity.CronExpression = cron;
        entity.JobParams = string.IsNullOrWhiteSpace(dto.JobParams) ? null : dto.JobParams.Trim();
        entity.MisfirePolicy = dto.MisfirePolicy;
        entity.Concurrent = dto.Concurrent;
        entity.Timeout = dto.Timeout;
        entity.RetryCount = dto.RetryCount;
        entity.RetryInterval = dto.RetryInterval;
        entity.Remark = string.IsNullOrWhiteSpace(dto.Remark) ? null : dto.Remark.Trim();
        entity.UpdateBy = _currentUser.UserName;
        entity.UpdateTime = DateTime.Now;

        await _jobRepository.SaveChangesAsync();

        try
        {
            if (nameChanged)
            {
                // 改名等于换了 JobKey：旧的必须整个删掉重建，否则新旧两份 Trigger 会同时跑
                await _scheduler.DeleteJob(JobKeyOf(entity.JobName, entity.JobGroup), CancellationToken.None);
                await ScheduleJobAsync(entity);
            }
            else
            {
                await RescheduleJobAsync(entity);
            }
        }
        catch (Exception ex)
        {
            // 只重建 Trigger 会失败在「JobDetail 已经不存在」上（qrtz_job_details 被清过、
            // 或调度器状态与库不一致）。先降级成整体重新排程补挂 JobDetail，再失败才算真失败。
            _logger.LogWarning(ex, "重建 Trigger 失败，尝试整体重新排程（jobId={JobId}）", entity.Id);
            try
            {
                await ScheduleJobAsync(entity);
                return ApiResponse<bool>.Success(true, "修改成功（调度器状态不一致，已自动重新排程）");
            }
            catch (Exception ex2)
            {
                _logger.LogError(ex2, "修改任务后重建 Quartz 调度失败（jobId={JobId}）", entity.Id);
                return ApiResponse<bool>.Success(true, "任务已保存，但调度器重建失败，请检查调度器状态");
            }
        }

        return ApiResponse<bool>.Success(true, "修改成功");
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<bool>> DeleteAsync(string ids)
    {
        var idList = ParseIds(ids);
        if (idList.Count == 0) return ApiResponse<bool>.Fail("请选择要删除的任务");

        var deleted = 0;
        foreach (var id in idList)
        {
            var job = await _jobRepository.GetByIdAsync(id);
            if (job is null) continue;

            // 顺序不能反：先摘 Quartz 再删库，避免留下没有 sys_job 对应的脏 Trigger
            try
            {
                await _scheduler.DeleteJob(JobKeyOf(job.JobName, job.JobGroup), CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "删除任务时清理 Quartz 调度失败（jobId={JobId}），继续删除数据库记录", id);
            }

            deleted++;
        }

        if (deleted == 0) return ApiResponse<bool>.Fail("任务不存在");

        await _jobRepository.DeleteAsync(idList);
        return ApiResponse<bool>.Success(true, "删除成功");
    }

    // ==================================================================
    // 三、启停与立即执行
    // ==================================================================

    /// <inheritdoc/>
    public async Task<ApiResponse<bool>> StartAsync(long id)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job is null) return ApiResponse<bool>.Fail("任务不存在");

        // 库里的 cron / job_type 可能在别处被改过，启动前重新校验一次
        EnsureCronValid(job.CronExpression);
        EnsureJobTypeValid(job.JobType);

        var jobKey = JobKeyOf(job.JobName, job.JobGroup);
        var triggerKey = TriggerKeyOf(job.JobName, job.JobGroup);

        try
        {
            if (await _scheduler.GetTrigger(triggerKey, CancellationToken.None) is not null)
            {
                // 已登记过 → 只需恢复；CRON 表达式若被手工改过也会随 Resume 生效
                await _scheduler.ResumeTrigger(triggerKey, CancellationToken.None);
            }
            else
            {
                // 从未登记过（例如手动改库后启动）→ 完整排程一次
                await ScheduleJobAsync(job);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动任务失败（jobId={JobId}）", id);
            return ApiResponse<bool>.Fail($"启动失败：{ex.Message}");
        }

        job.Status = 1;
        job.UpdateBy = _currentUser.UserName;
        job.UpdateTime = DateTime.Now;
        await _jobRepository.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, "已启动");
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<bool>> StopAsync(long id)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job is null) return ApiResponse<bool>.Fail("任务不存在");

        var triggerKey = TriggerKeyOf(job.JobName, job.JobGroup);
        try
        {
            if (await _scheduler.GetTrigger(triggerKey, CancellationToken.None) is not null)
            {
                await _scheduler.PauseTrigger(triggerKey, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "停止任务时暂停 Quartz 触发器失败（jobId={JobId}）", id);
        }

        job.Status = 0;
        job.UpdateBy = _currentUser.UserName;
        job.UpdateTime = DateTime.Now;
        await _jobRepository.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, "已停止");
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<JobRunResultDto>> RunAsync(long id)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job is null) return ApiResponse<JobRunResultDto>.Fail("任务不存在");

        var jobKey = JobKeyOf(job.JobName, job.JobGroup);
        if (await _scheduler.GetJobDetail(jobKey, CancellationToken.None) is null)
        {
            return ApiResponse<JobRunResultDto>.Fail("该任务尚未调度到调度器，请先启动任务再执行");
        }

        try
        {
            // 业务参数随 JobDetail 一起生效，这里不再额外传 JobDataMap（避免覆盖 trigger 上的重试参数）
            await _scheduler.TriggerJob(jobKey, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "立即执行任务失败（jobId={JobId}）", id);
            return ApiResponse<JobRunResultDto>.Fail($"触发失败：{ex.Message}");
        }

        return ApiResponse<JobRunResultDto>.Success(new JobRunResultDto
        {
            JobId = job.Id,
            JobName = job.JobName,
            FireTime = DateTime.Now,
            TriggerType = "立即执行",
        }, "已触发执行");
    }

    // ==================================================================
    // 四、执行日志
    // ==================================================================

    /// <inheritdoc/>
    public async Task<ApiResponse<PagedResult<JobLogDto>>> GetLogPagedAsync(JobLogQueryDto query)
    {
        if (query.Page < 1) query.Page = 1;
        if (query.PageSize < 1 || query.PageSize > 200) query.PageSize = 10;

        var (items, total) = await _logRepository.GetPagedAsync(query);
        var list = _mapper.Map<List<JobLogDto>>(items);
        foreach (var dto in list) dto.StatusText = JobTexts.LogStatus(dto.Status);

        return ApiResponse<PagedResult<JobLogDto>>.Success(
            PagedResult<JobLogDto>.Create(list, total, query.Page, query.PageSize),
            "查询成功");
    }

    // ==================================================================
    // 五、Quartz 联动
    // ==================================================================

    /// <summary>完整排程：登记 JobDetail + 挂 Cron Trigger</summary>
    private async Task ScheduleJobAsync(SysJob job)
    {
        var jobKey = JobKeyOf(job.JobName, job.JobGroup);
        var triggerKey = TriggerKeyOf(job.JobName, job.JobGroup);

        if (await _scheduler.GetTrigger(triggerKey, CancellationToken.None) is not null)
        {
            await _scheduler.UnscheduleJob(triggerKey, CancellationToken.None);
        }

        var jobDetail = BuildJobDetail(job);
        // replace=true：任务名/分组被改过但旧 JobDetail 还残留时，用新的覆盖
        await _scheduler.AddJob(jobDetail, replace: true, CancellationToken.None);
        await _scheduler.ScheduleJob(BuildCronTrigger(job), CancellationToken.None);

        _logger.LogInformation("任务已排程到 Quartz：{JobKey}，cron={Cron}", jobKey, job.CronExpression);
    }

    /// <summary>只重建 Trigger（用于改 Cron / 参数 / 策略，JobKey 未变）</summary>
    private async Task RescheduleJobAsync(SysJob job)
    {
        var triggerKey = TriggerKeyOf(job.JobName, job.JobGroup);

        if (await _scheduler.GetTrigger(triggerKey, CancellationToken.None) is not null)
        {
            await _scheduler.UnscheduleJob(triggerKey, CancellationToken.None);
        }

        await _scheduler.ScheduleJob(BuildCronTrigger(job), CancellationToken.None);
        _logger.LogInformation("任务 Trigger 已重建：{TriggerKey}，cron={Cron}", triggerKey, job.CronExpression);
    }

    // ------------------------------------------------------------------

    private static JobKey JobKeyOf(string jobName, string jobGroup) => new(jobName, jobGroup);

    private static TriggerKey TriggerKeyOf(string jobName, string jobGroup)
        => new($"{jobName}_trigger", jobGroup);

    /// <summary>把 job_params 的 JSON 转成 Quartz JobDataMap；数字统一按字符串存，读回时再解析</summary>
    private static JobDataMap BuildJobDataMap(SysJob job)
    {
        var map = new JobDataMap();

        if (!string.IsNullOrWhiteSpace(job.JobParams))
        {
            try
            {
                using var doc = JsonDocument.Parse(job.JobParams);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in doc.RootElement.EnumerateObject())
                    {
                        map.Put(p.Name, p.Value.ValueKind switch
                        {
                            JsonValueKind.Number => p.Value.GetRawText(),
                            JsonValueKind.True => "true",
                            JsonValueKind.False => "false",
                            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                            _ => p.Value.GetString() ?? string.Empty,
                        });
                    }
                }
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"任务参数不是合法 JSON：{ex.Message}");
            }
        }

        // 重试参数：监听器在失败时读这两个键决定要不要补跑
        map.Put(RetryLeftKey, job.RetryCount);
        map.Put(RetryIntervalKey, job.RetryInterval);

        return map;
    }

    private static IJobDetail BuildJobDetail(SysJob job)
    {
        var jobType = ResolveJobType(job.JobType)
                      ?? throw new InvalidOperationException($"任务类型不存在：{job.JobType}");

        var builder = JobBuilder.Create(jobType)
            .WithIdentity(JobKeyOf(job.JobName, job.JobGroup))
            .WithDescription(job.Remark)
            .SetJobData(BuildJobDataMap(job))
            // 必须持久化：否则删掉 Trigger 后 Quartz 会把没有 Trigger 的 Job 一并清掉
            .StoreDurably();

        // concurrent=0 → 禁止并发（等价于 @DisallowConcurrentExecution）
        if (job.Concurrent != 1)
        {
            builder.DisallowConcurrentExecution();
        }

        return builder.Build();
    }

    private static ITrigger BuildCronTrigger(SysJob job)
    {
        var schedule = CronScheduleBuilder.CronSchedule(job.CronExpression);

        // CronTrigger 只有三种错过语义（Quartz.NET 3.x 的实例方法，无泛型重载）：
        //   FireAndProceed  = 立即补跑一次并继续按 Cron 走（对应策略 0/1）
        //   IgnoreMisfires  = 直接丢弃这次错过（对应策略 2）
        schedule = job.MisfirePolicy >= 2
            ? schedule.WithMisfireHandlingInstructionIgnoreMisfires()
            : schedule.WithMisfireHandlingInstructionFireAndProceed();

        return TriggerBuilder.Create()
            .WithIdentity(TriggerKeyOf(job.JobName, job.JobGroup))
            .ForJob(JobKeyOf(job.JobName, job.JobGroup))
            .WithSchedule(schedule)
            .Build();
    }

    // ------------------------------------------------------------------

    /// <summary>Cron 校验：非法表达式直接拒绝，不让非法值进调度器</summary>
    private static void EnsureCronValid(string cron)
    {
        try
        {
            _ = new CronExpression(cron);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Cron 表达式非法：{ex.Message}");
        }
    }

    /// <summary>反射解析 job_type（允许只写类名，会自动补本程序集名）</summary>
    private static Type? ResolveJobType(string jobType)
    {
        var type = Type.GetType(jobType, throwOnError: false);
        if (type is not null) return type;

        var assemblyName = typeof(JobService).Assembly.GetName().Name;
        return Type.GetType($"{jobType}, {assemblyName}", throwOnError: false);
    }

    /// <summary>job_type 存在性 + 合法性校验，避免注册不存在的类导致运行时炸</summary>
    private static void EnsureJobTypeValid(string jobType)
    {
        var type = ResolveJobType(jobType);

        if (type is null)
        {
            throw new InvalidOperationException(
                $"任务类型「{jobType}」在本程序中不存在（job_type 需为 IJob 实现类的全限定类名）");
        }

        if (!typeof(IJob).IsAssignableFrom(type))
        {
            throw new InvalidOperationException($"任务类型「{jobType}」未实现 Quartz.IJob 接口");
        }

        if (type.IsAbstract || type.IsInterface)
        {
            throw new InvalidOperationException($"任务类型「{jobType}」不能是抽象类或接口");
        }
    }

    private static List<long> ParseIds(string ids)
    {
        var result = new List<long>();
        if (string.IsNullOrWhiteSpace(ids)) return result;

        foreach (var part in ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (long.TryParse(part, out var id) && id > 0) result.Add(id);
        }

        return result;
    }

    // ==================================================================
    // 三、启动预热（由 JobSchedulePreloadService 调用，见文档「启动预热机制」）
    // ==================================================================

    /// <summary>
    /// 把 sys_job 里 status=1 的任务逐个注册到 Quartz 调度器。
    ///
    /// ⚠️ 每个任务单独 try-catch：某个任务失败（Cron 非法 / 类不存在 / 表缺列）绝不能
    ///    影响其余任务的加载，否则一次脏数据会导致整个应用起不来（文档避坑第 6 条）。
    /// </summary>
    /// <returns>预热成功的任务数</returns>
    public async Task<int> PreloadEnabledJobsAsync()
    {
        var all = new List<SysJob>();
        // 分页拉取，避免任务多时一次掏空内存；单次最多 200 条，够用
        for (var page = 1; page <= 5; page++)
        {
            var (items, total) = await _jobRepository.GetPagedAsync(
                new JobQueryDto { Page = page, PageSize = 200 });

            all.AddRange(items);
            if (all.Count >= total) break;
        }

        var enabled = all.Where(j => j.Status == 1).ToList();
        var ok = 0;

        foreach (var job in enabled)
        {
            try
            {
                await ScheduleJobAsync(job);
                ok++;
            }
            catch (Exception ex)
            {
                // 预热失败只记日志，不中断启动
                _logger.LogError(ex, "任务预热失败，已跳过：jobId={JobId} jobName={JobName}",
                    job.Id, job.JobName);
            }
        }

        if (enabled.Count > 0)
        {
            _logger.LogInformation("定时任务预热完成：共 {Total} 个启用任务，成功 {Ok} 个",
                enabled.Count, ok);
        }

        return ok;
    }
}
