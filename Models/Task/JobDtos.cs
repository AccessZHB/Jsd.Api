namespace Jsd.Api.Models.Task;

/// <summary>
/// 自动任务模块 —— 按钮级权限标识常量
/// 与 sys_menu.permission（1103~1109）、前端 v-permission 三处保持一致，改这里必须同步改另外两处。
/// </summary>
/// <remarks>
/// 本项目后端只挂 [Authorize] 做登录校验，按钮级权限由前端 v-permission 指令消费这些标识。
/// 对应 sys_menu 记录：1103 add / 1104 edit / 1105 remove / 1106 start / 1107 stop / 1108 run / 1109 log。
/// </remarks>
public static class TaskJobPermissions
{
    /// <summary>任务列表查看</summary>
    public const string List = "task:job:list";

    /// <summary>新增任务</summary>
    public const string Add = "task:job:add";

    /// <summary>编辑任务</summary>
    public const string Edit = "task:job:edit";

    /// <summary>删除任务</summary>
    public const string Remove = "task:job:remove";

    /// <summary>启动任务</summary>
    public const string Start = "task:job:start";

    /// <summary>停止任务</summary>
    public const string Stop = "task:job:stop";

    /// <summary>立即执行</summary>
    public const string Run = "task:job:run";

    /// <summary>执行日志查看</summary>
    public const string Log = "task:job:log";
}

/// <summary>自动任务模块 —— 枚举文案（后端出参直接带中文，前端不必再映射）</summary>
public static class JobTexts
{
    /// <summary>sys_job.status 文案：0-停止 1-运行中</summary>
    public static string JobStatus(int status) => status == 1 ? "运行中" : "停止";

    /// <summary>sys_job.misfire_policy 文案：0-立即执行 1-执行一次 2-忽略</summary>
    public static string MisfirePolicy(int p) => p switch
    {
        0 => "立即执行",
        1 => "执行一次",
        2 => "忽略",
        _ => "忽略",
    };

    /// <summary>sys_job.concurrent 文案：0-禁止并发 1-允许并发</summary>
    public static string Concurrent(int c) => c == 1 ? "允许" : "禁止";

    /// <summary>sys_job_log.status 文案：1-成功 0-失败</summary>
    public static string LogStatus(int s) => s == 1 ? "成功" : "失败";
}

/// <summary>定时任务 —— 分页查询条件（GET /api/task/job/list）</summary>
public class JobQueryDto
{
    /// <summary>任务名称模糊匹配</summary>
    public string? JobName { get; set; }

    /// <summary>任务分组精确匹配（不传=全部）</summary>
    public string? JobGroup { get; set; }

    /// <summary>状态：0-停止 1-运行中（不传=全部）</summary>
    public int? Status { get; set; }

    /// <summary>页码，从 1 开始</summary>
    public int Page { get; set; } = 1;

    /// <summary>每页条数，最大 200</summary>
    public int PageSize { get; set; } = 10;
}

/// <summary>定时任务 —— 列表/详情出参（GET /api/task/job/list、/{id}）</summary>
public class JobDto
{
    public long Id { get; set; }

    /// <summary>任务名称</summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>任务分组</summary>
    public string JobGroup { get; set; } = "DEFAULT";

    /// <summary>任务类型（全限定类名）</summary>
    public string JobType { get; set; } = string.Empty;

    /// <summary>Cron 表达式</summary>
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>任务参数（JSON 字符串）</summary>
    public string? JobParams { get; set; }

    /// <summary>状态：0-停止 1-运行中</summary>
    public int Status { get; set; }

    /// <summary>状态中文文案（运行中 / 停止）</summary>
    public string StatusText { get; set; } = string.Empty;

    /// <summary>丢失策略：0-立即执行 1-执行一次 2-忽略</summary>
    public int MisfirePolicy { get; set; }

    /// <summary>丢失策略中文文案</summary>
    public string MisfirePolicyText { get; set; } = string.Empty;

    /// <summary>是否允许并发：0-禁止 1-允许</summary>
    public int Concurrent { get; set; }

    /// <summary>并发控制中文文案（禁止 / 允许）</summary>
    public string ConcurrentText { get; set; } = string.Empty;

    /// <summary>任务超时时间（秒），0=不限制</summary>
    public int Timeout { get; set; }

    /// <summary>失败重试次数</summary>
    public int RetryCount { get; set; }

    /// <summary>失败重试间隔（秒）</summary>
    public int RetryInterval { get; set; }

    /// <summary>下次触发时间（Quartz 实时计算；未调度或已停止时为 null）</summary>
    public DateTime? NextValidTime { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    public string CreateBy { get; set; } = string.Empty;

    public DateTime CreateTime { get; set; }

    public string UpdateBy { get; set; } = string.Empty;

    public DateTime UpdateTime { get; set; }
}

/// <summary>定时任务 —— 新增入参（POST /api/task/job）</summary>
public class CreateJobDto
{
    /// <summary>任务名称（同一分组下不重复）</summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>任务分组，默认 DEFAULT</summary>
    public string JobGroup { get; set; } = "DEFAULT";

    /// <summary>任务类型（全限定类名，如 Jsd.Api.Jobs.LogCleanJob, Jsd.Api）</summary>
    public string JobType { get; set; } = string.Empty;

    /// <summary>Cron 表达式（Quartz 7 位，含秒）</summary>
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>任务参数（JSON 字符串，可为空）</summary>
    public string? JobParams { get; set; }

    /// <summary>状态：0-停止 1-运行中（新增后由 Service 决定是否立即调度）</summary>
    public int Status { get; set; }

    /// <summary>丢失策略：0-立即执行 1-执行一次 2-忽略</summary>
    public int MisfirePolicy { get; set; } = 1;

    /// <summary>是否允许并发：0-禁止 1-允许</summary>
    public int Concurrent { get; set; }

    /// <summary>任务超时（秒），0=不限制</summary>
    public int Timeout { get; set; }

    /// <summary>失败重试次数</summary>
    public int RetryCount { get; set; }

    /// <summary>失败重试间隔（秒）</summary>
    public int RetryInterval { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}

/// <summary>定时任务 —— 修改入参（PUT /api/task/job）</summary>
public class UpdateJobDto
{
    /// <summary>任务主键</summary>
    public long Id { get; set; }

    /// <summary>任务名称（同一分组下不重复）</summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>任务分组（修改分组会导致 Quartz 侧重建 Trigger）</summary>
    public string JobGroup { get; set; } = "DEFAULT";

    /// <summary>任务类型（全限定类名）</summary>
    public string JobType { get; set; } = string.Empty;

    /// <summary>Cron 表达式</summary>
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>任务参数（JSON 字符串，可为空）</summary>
    public string? JobParams { get; set; }

    /// <summary>丢失策略</summary>
    public int MisfirePolicy { get; set; } = 1;

    /// <summary>是否允许并发</summary>
    public int Concurrent { get; set; }

    /// <summary>任务超时（秒）</summary>
    public int Timeout { get; set; }

    /// <summary>失败重试次数</summary>
    public int RetryCount { get; set; }

    /// <summary>失败重试间隔（秒）</summary>
    public int RetryInterval { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}

/// <summary>定时任务 —— 执行日志查询条件（GET /api/task/job/log）</summary>
public class JobLogQueryDto
{
    /// <summary>任务ID精确匹配（不传=全部）</summary>
    public long? JobId { get; set; }

    /// <summary>任务名称模糊匹配（不传=全部）</summary>
    public string? JobName { get; set; }

    /// <summary>执行状态：1-成功 0-失败（不传=全部）</summary>
    public int? Status { get; set; }

    /// <summary>触发时间起（含）</summary>
    public DateTime? FireTimeStart { get; set; }

    /// <summary>触发时间止（含）</summary>
    public DateTime? FireTimeEnd { get; set; }

    /// <summary>页码，从 1 开始</summary>
    public int Page { get; set; } = 1;

    /// <summary>每页条数，最大 200</summary>
    public int PageSize { get; set; } = 10;
}

/// <summary>定时任务 —— 执行日志出参（GET /api/task/job/log）</summary>
public class JobLogDto
{
    public long Id { get; set; }

    /// <summary>关联任务ID（任务被删后保留痕迹）</summary>
    public long? JobId { get; set; }

    public string JobName { get; set; } = string.Empty;

    public string JobGroup { get; set; } = "DEFAULT";

    /// <summary>Quartz 触发器名称</summary>
    public string? TriggerName { get; set; }

    /// <summary>Quartz 触发器分组</summary>
    public string? TriggerGroup { get; set; }

    /// <summary>触发时间</summary>
    public DateTime FireTime { get; set; }

    /// <summary>开始执行时间</summary>
    public DateTime StartTime { get; set; }

    /// <summary>结束执行时间</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>执行耗时（毫秒）</summary>
    public long Duration { get; set; }

    /// <summary>执行状态：1-成功 0-失败</summary>
    public int Status { get; set; }

    /// <summary>执行状态中文文案（成功 / 失败）</summary>
    public string StatusText { get; set; } = string.Empty;

    /// <summary>执行结果摘要</summary>
    public string? ExecuteResult { get; set; }

    /// <summary>异常信息</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>定时任务 —— 立即执行返回结果（POST /api/task/job/run/{id}）</summary>
public class JobRunResultDto
{
    /// <summary>任务ID</summary>
    public long JobId { get; set; }

    /// <summary>任务名称</summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>触发时间（本次立即触发的 fire_time）</summary>
    public DateTime FireTime { get; set; }

    /// <summary>触发方式：立即执行 / 定时触发</summary>
    public string TriggerType { get; set; } = "立即执行";
}
