using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.System;
using Jsd.Api.Repositories;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Jsd.Api.Services;

/// <summary>
/// 系统管理·日志服务接口（操作日志 / 登录日志 / 已锁定账号）
/// 删除、清理、解锁等高危操作仅允许超级管理员（is_super=1）。
/// </summary>
public interface ISystemLogService
{
    // ---------- 操作日志 ----------
    Task<ApiResponse<PagedResult<OperationLogDto>>> GetOperationLogsAsync(OperationLogQueryDto query);
    Task<ApiResponse<OperationLogDetailDto>> GetOperationLogDetailAsync(long id);
    Task<ApiResponse<bool>> DeleteOperationLogAsync(long id);
    Task<ApiResponse<bool>> DeleteOperationLogBatchAsync(BatchDeleteDto dto);
    Task<ApiResponse<int>> CleanOperationLogsAsync(CleanLogDto dto);

    /// <summary>导出操作日志 Excel（返回文件字节 + 文件名，Controller 以 FileContentResult 下发）</summary>
    Task<(byte[] Content, string FileName)> ExportOperationLogsAsync(OperationLogQueryDto query);

    // ---------- 登录日志 ----------
    Task<ApiResponse<PagedResult<LoginLogDto>>> GetLoginLogsAsync(LoginLogQueryDto query);
    Task<ApiResponse<LoginLogDto>> GetLoginLogDetailAsync(long id);
    Task<ApiResponse<bool>> DeleteLoginLogAsync(long id);
    Task<ApiResponse<bool>> DeleteLoginLogBatchAsync(BatchDeleteDto dto);
    Task<ApiResponse<int>> CleanLoginLogsAsync(CleanLogDto dto);

    /// <summary>导出登录日志 Excel（返回文件字节 + 文件名，Controller 以 FileContentResult 下发）</summary>
    Task<(byte[] Content, string FileName)> ExportLoginLogsAsync(LoginLogQueryDto query);

    // ---------- 已锁定账号 ----------
    Task<ApiResponse<PagedResult<LockAccountDto>>> GetLockedAccountsAsync(LockAccountQueryDto query);

    // ---------- 统计概览（前端页面顶部统计卡片）----------
    /// <summary>操作日志统计：今日总数 / 成功 / 失败 / 平均耗时</summary>
    Task<ApiResponse<OperationLogStatisticsDto>> GetOperationLogStatisticsAsync();

    /// <summary>登录日志统计：今日总数 / 成功 / 失败 / 当前锁定账号数</summary>
    Task<ApiResponse<LoginLogStatisticsDto>> GetLoginLogStatisticsAsync();
}

/// <summary>
/// 系统管理·日志服务实现
/// </summary>
public class SystemLogService : ISystemLogService
{
    private readonly ISysOperationLogRepository _operationLogRepository;
    private readonly ISysLoginLogRepository _loginLogRepository;
    private readonly ISysUserRepository _userRepository;
    private readonly ISecurityService _securityService;
    private readonly CurrentUserService _currentUser;

    public SystemLogService(
        ISysOperationLogRepository operationLogRepository,
        ISysLoginLogRepository loginLogRepository,
        ISysUserRepository userRepository,
        ISecurityService securityService,
        CurrentUserService currentUser)
    {
        _operationLogRepository = operationLogRepository;
        _loginLogRepository = loginLogRepository;
        _userRepository = userRepository;
        _securityService = securityService;
        _currentUser = currentUser;
    }

    // ============================================================
    // 操作日志
    // ============================================================

    /// <inheritdoc/>
    public async Task<ApiResponse<PagedResult<OperationLogDto>>> GetOperationLogsAsync(OperationLogQueryDto query)
    {
        NormalizePage(query);
        var (items, total) = await _operationLogRepository.GetPagedListAsync(query);
        return ApiResponse<PagedResult<OperationLogDto>>.Success(
            PagedResult<OperationLogDto>.Create(items.Select(ToOperationLogDto).ToList(), total, query.Page, query.PageSize));
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<OperationLogDetailDto>> GetOperationLogDetailAsync(long id)
    {
        var log = await _operationLogRepository.GetByIdAsync(id);
        if (log == null)
            return ApiResponse<OperationLogDetailDto>.Fail("操作日志不存在");
        return ApiResponse<OperationLogDetailDto>.Success(ToOperationLogDetailDto(log));
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<bool>> DeleteOperationLogAsync(long id)
    {
        var deny = CheckSuperAdmin<bool>();
        if (deny != null) return deny;

        var ok = await _operationLogRepository.DeleteAsync(id);
        return ok
            ? ApiResponse<bool>.Success(true, "删除成功")
            : ApiResponse<bool>.Fail("操作日志不存在");
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<bool>> DeleteOperationLogBatchAsync(BatchDeleteDto dto)
    {
        var deny = CheckSuperAdmin<bool>();
        if (deny != null) return deny;

        var count = await _operationLogRepository.DeleteBatchAsync(dto.Ids);
        return ApiResponse<bool>.Success(true, $"已删除 {count} 条操作日志");
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<int>> CleanOperationLogsAsync(CleanLogDto dto)
    {
        var deny = CheckSuperAdmin<int>();
        if (deny != null) return deny;

        // 操作日志默认保留 90 天（文档 5-5 / 九-7）
        var days = dto.Days > 0 ? dto.Days : 90;
        var count = await _operationLogRepository.CleanBeforeAsync(DateTime.Now.AddDays(-days));
        return ApiResponse<int>.Success(count, $"已清理 {count} 条过期操作日志（保留 {days} 天）");
    }

    /// <summary>
    /// 导出操作日志 Excel（.xlsx，NPOI 生成，筛选条件同分页查询）
    /// 返回文件字节 + 文件名；由 Controller 以 FileContentResult 下发。
    /// </summary>
    public async Task<(byte[] Content, string FileName)> ExportOperationLogsAsync(OperationLogQueryDto query)
    {
        var logs = await _operationLogRepository.GetListForExportAsync(query);

        IWorkbook workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("操作日志");

        // 表头样式（加粗）
        var headerStyle = workbook.CreateCellStyle();
        var font = workbook.CreateFont();
        font.IsBold = true;
        headerStyle.SetFont(font);

        var headers = new[] { "ID", "操作人", "模块", "动作", "请求方法", "接口路径", "IP地址", "浏览器", "操作系统", "耗时(ms)", "状态", "操作时间" };
        var headerRow = sheet.CreateRow(0);
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = headerRow.CreateCell(i);
            cell.SetCellValue(headers[i]);
            cell.CellStyle = headerStyle;
        }

        for (var r = 0; r < logs.Count; r++)
        {
            var row = sheet.CreateRow(r + 1);
            var log = logs[r];
            row.CreateCell(0).SetCellValue(log.Id);
            row.CreateCell(1).SetCellValue(log.OperatorName);
            row.CreateCell(2).SetCellValue(log.Module);
            row.CreateCell(3).SetCellValue(log.Action);
            row.CreateCell(4).SetCellValue(log.RequestMethod);
            row.CreateCell(5).SetCellValue(log.Path);
            row.CreateCell(6).SetCellValue(log.IpAddress);
            row.CreateCell(7).SetCellValue(log.Browser);
            row.CreateCell(8).SetCellValue(log.Os);
            row.CreateCell(9).SetCellValue(log.ExecutionTime);
            row.CreateCell(10).SetCellValue(log.Status == 1 ? "成功" : "失败");
            row.CreateCell(11).SetCellValue(log.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        using var ms = new MemoryStream();
        workbook.Write(ms, leaveOpen: true);
        var fileName = $"操作日志_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
        return (ms.ToArray(), fileName);
    }

    // ============================================================
    // 登录日志
    // ============================================================

    /// <inheritdoc/>
    public async Task<ApiResponse<PagedResult<LoginLogDto>>> GetLoginLogsAsync(LoginLogQueryDto query)
    {
        NormalizePage(query);
        var (items, total) = await _loginLogRepository.GetPagedListAsync(query);
        return ApiResponse<PagedResult<LoginLogDto>>.Success(
            PagedResult<LoginLogDto>.Create(items.Select(ToLoginLogDto).ToList(), total, query.Page, query.PageSize));
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<LoginLogDto>> GetLoginLogDetailAsync(long id)
    {
        var log = await _loginLogRepository.GetByIdAsync(id);
        if (log == null)
            return ApiResponse<LoginLogDto>.Fail("登录日志不存在");
        return ApiResponse<LoginLogDto>.Success(ToLoginLogDto(log));
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<bool>> DeleteLoginLogAsync(long id)
    {
        var deny = CheckSuperAdmin<bool>();
        if (deny != null) return deny;

        var ok = await _loginLogRepository.DeleteAsync(id);
        return ok
            ? ApiResponse<bool>.Success(true, "删除成功")
            : ApiResponse<bool>.Fail("登录日志不存在");
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<bool>> DeleteLoginLogBatchAsync(BatchDeleteDto dto)
    {
        var deny = CheckSuperAdmin<bool>();
        if (deny != null) return deny;

        var count = await _loginLogRepository.DeleteBatchAsync(dto.Ids);
        return ApiResponse<bool>.Success(true, $"已删除 {count} 条登录日志");
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<int>> CleanLoginLogsAsync(CleanLogDto dto)
    {
        var deny = CheckSuperAdmin<int>();
        if (deny != null) return deny;

        // 登录日志保留周期更长：默认 180 天（文档 5-11）
        var days = dto.Days > 0 ? dto.Days : 180;
        var count = await _loginLogRepository.CleanBeforeAsync(DateTime.Now.AddDays(-days));
        return ApiResponse<int>.Success(count, $"已清理 {count} 条过期登录日志（保留 {days} 天）");
    }

    // ============================================================
    // 已锁定账号
    // ============================================================

    /// <inheritdoc/>
    public async Task<ApiResponse<PagedResult<LockAccountDto>>> GetLockedAccountsAsync(LockAccountQueryDto query)
    {
        NormalizePage(query);
        var (items, total) = await _userRepository.GetLockedAccountsAsync(
            query.MemberName, query.IpAddress, query.Page, query.PageSize);

        // 触发锁定的登录方式：取该账号最近一条失败登录日志的 login_type
        var failTypes = await _loginLogRepository.GetLastFailLoginTypesAsync(items.Select(u => u.Id).ToList());
        var failTypeMap = failTypes.ToDictionary(x => x.MemberId, x => x.LoginType);

        // 锁定开始时间 = 锁定截止时间 - 锁定时长（sys_user 未单独存开始时间，按策略回推）
        var config = await _securityService.GetConfigAsync();

        var list = items.Select(u => new LockAccountDto
        {
            MemberId = u.Id,
            MemberName = u.UserName,
            RealName = u.RealName,
            FailCount = u.FailCount,
            LockUntil = u.LockUntil,
            LockStartTime = u.LockUntil?.AddMinutes(-config.LockDurationMinutes),
            LoginType = failTypeMap.TryGetValue(u.Id, out var lt) ? lt : null,
            LastLoginIp = u.LastLoginIp,
            LastLoginTime = u.LastLoginTime
        }).ToList();

        return ApiResponse<PagedResult<LockAccountDto>>.Success(
            PagedResult<LockAccountDto>.Create(list, total, query.Page, query.PageSize));
    }

    /// <summary>
    /// 导出登录日志 Excel（.xlsx，NPOI 生成，筛选条件同分页查询）
    /// </summary>
    public async Task<(byte[] Content, string FileName)> ExportLoginLogsAsync(LoginLogQueryDto query)
    {
        var logs = await _loginLogRepository.GetListForExportAsync(query);

        IWorkbook workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("登录日志");

        var headerStyle = workbook.CreateCellStyle();
        var font = workbook.CreateFont();
        font.IsBold = true;
        headerStyle.SetFont(font);

        var headers = new[] { "ID", "用户名", "登录方式", "登录IP", "登录地点", "浏览器", "操作系统", "状态", "失败原因", "登录时间" };
        var headerRow = sheet.CreateRow(0);
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = headerRow.CreateCell(i);
            cell.SetCellValue(headers[i]);
            cell.CellStyle = headerStyle;
        }

        for (var r = 0; r < logs.Count; r++)
        {
            var row = sheet.CreateRow(r + 1);
            var log = logs[r];
            row.CreateCell(0).SetCellValue(log.Id);
            row.CreateCell(1).SetCellValue(log.MemberName);
            row.CreateCell(2).SetCellValue(LoginTypeText(log.LoginType));
            row.CreateCell(3).SetCellValue(log.IpAddress);
            row.CreateCell(4).SetCellValue(log.LoginLocation);
            row.CreateCell(5).SetCellValue(log.Browser);
            row.CreateCell(6).SetCellValue(log.Os);
            row.CreateCell(7).SetCellValue(log.Status == 1 ? "成功" : "失败");
            row.CreateCell(8).SetCellValue(log.FailReason);
            row.CreateCell(9).SetCellValue(log.LoginTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "");
        }

        using var ms = new MemoryStream();
        workbook.Write(ms, leaveOpen: true);
        var fileName = $"登录日志_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
        return (ms.ToArray(), fileName);
    }

    // ============================================================
    // 统计概览
    // ============================================================

    /// <inheritdoc/>
    public async Task<ApiResponse<OperationLogStatisticsDto>> GetOperationLogStatisticsAsync()
    {
        var stat = await _operationLogRepository.GetStatisticsAsync(DateTime.Today);
        return ApiResponse<OperationLogStatisticsDto>.Success(stat);
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<LoginLogStatisticsDto>> GetLoginLogStatisticsAsync()
    {
        var stat = await _loginLogRepository.GetStatisticsAsync(DateTime.Today);
        return ApiResponse<LoginLogStatisticsDto>.Success(stat);
    }

    // ============================================================
    // 内部工具
    // ============================================================

    /// <summary>高危操作守卫：仅超级管理员（is_super=1，JWT claim）；非超管返回失败响应，否则 null</summary>
    private ApiResponse<T>? CheckSuperAdmin<T>()
    {
        return _currentUser.IsSuper ? null : ApiResponse<T>.Fail("仅超级管理员可执行此操作", 403);
    }

    /// <summary>归一化分页参数（Page 从 1 起，PageSize 1~100）</summary>
    private static void NormalizePage(ISystemPagedQuery query)
    {
        if (query.Page < 1) query.Page = 1;
        if (query.PageSize < 1) query.PageSize = 10;
        if (query.PageSize > 100) query.PageSize = 100;
    }

    private static OperationLogDto ToOperationLogDto(SysOperationLog log) => new()
    {
        Id = log.Id,
        OperatorId = log.OperatorId,
        OperatorName = log.OperatorName,
        Module = log.Module,
        Action = log.Action,
        RequestMethod = log.RequestMethod,
        Path = log.Path,
        IpAddress = log.IpAddress,
        Browser = log.Browser,
        Os = log.Os,
        ExecutionTime = log.ExecutionTime,
        Status = log.Status,
        CreateTime = log.CreateTime
    };

    private static OperationLogDetailDto ToOperationLogDetailDto(SysOperationLog log)
    {
        var dto = new OperationLogDetailDto
        {
            Id = log.Id,
            OperatorId = log.OperatorId,
            OperatorName = log.OperatorName,
            Module = log.Module,
            Action = log.Action,
            RequestMethod = log.RequestMethod,
            Path = log.Path,
            IpAddress = log.IpAddress,
            Browser = log.Browser,
            Os = log.Os,
            ExecutionTime = log.ExecutionTime,
            Status = log.Status,
            CreateTime = log.CreateTime,
            RequestParams = log.RequestParams,
            ResponseResult = log.ResponseResult,
            UserAgent = log.UserAgent,
            ErrorMessage = log.ErrorMessage
        };
        return dto;
    }

    /// <summary>登录方式字典：1-账号密码 2-微信授权 3-短信验证码（与前端 types/system.ts 一致）</summary>
    private static string LoginTypeText(int loginType) => loginType switch
    {
        1 => "账号密码",
        2 => "微信授权",
        3 => "短信验证码",
        _ => "未知"
    };

    private static LoginLogDto ToLoginLogDto(SysLoginLog log) => new()
    {
        Id = log.Id,
        MemberId = log.MemberId,
        MemberName = log.MemberName,
        LoginType = log.LoginType,
        IpAddress = log.IpAddress,
        Browser = log.Browser,
        Os = log.Os,
        LoginLocation = log.LoginLocation,
        Status = log.Status,
        FailReason = log.FailReason,
        LoginTime = log.LoginTime,
        CreateTime = log.CreateTime
    };
}
