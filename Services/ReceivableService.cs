using AutoMapper;
using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Finance;
using Jsd.Api.Repositories;

namespace Jsd.Api.Services;

/// <summary>
/// 应收账款 服务实现（菜单 321）
///
/// 【数据来源】trx_receivable 由"信用订单发货"自动生成，也允许财务手工调整。
/// 【金额自洽】unpaid_amount = total_amount - paid_amount，三者在核销时同步维护。
/// </summary>
public class ReceivableService : IReceivableService
{
    private readonly IReceivableRepository _receivableRepository;
    private readonly IRepository<TrxOrder> _orderRepository;
    private readonly IRepository<MemMember> _memberRepository;
    private readonly IMapper _mapper;

    public ReceivableService(
        IReceivableRepository receivableRepository,
        IRepository<TrxOrder> orderRepository,
        IRepository<MemMember> memberRepository,
        IMapper mapper)
    {
        _receivableRepository = receivableRepository;
        _orderRepository = orderRepository;
        _memberRepository = memberRepository;
        _mapper = mapper;
    }

    /// <summary>
    /// 分页查询应收账款列表（客户名称 / 订单号 / 状态 / 订单日期范围）
    /// </summary>
    public async Task<PagedResult<ReceivableListDto>> GetListAsync(ReceivablePageQuery query)
    {
        // 页码兜底，防止前端传 0 或负数导致 Skip 抛异常
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 10 : query.PageSize;

        var (rows, total) = await _receivableRepository.GetPagedListAsync(
            query.MemberId, query.MemberName, query.OrderNo, query.Status,
            query.DueStart, query.DueEnd, page, pageSize);

        var list = rows.Select(row =>
        {
            // AutoMapper 负责实体字段与派生字段（状态名/账龄/是否逾期）的映射
            var dto = _mapper.Map<ReceivableListDto>(row.Receivable);
            // 联查补全的展示字段（实体上没有，手工赋值）
            dto.OrderNo = row.OrderNo;
            dto.MemberName = row.MemberName;
            // 派生应收单号：AR + ID 补零 8 位（表内无 receivable_no 字段）
            dto.ReceivableNo = ReceivableNoFormatter.Format(dto.Id);
            return dto;
        }).ToList();

        return PagedResult<ReceivableListDto>.Create(list, total, page, pageSize);
    }

    /// <summary>
    /// 应收详情 = 主信息 + 该笔应收的核销记录（Join mkt_payment_item + mkt_payment）
    /// </summary>
    public async Task<ReceivableDetailDto?> GetDetailAsync(long id)
    {
        var receivable = await _receivableRepository.GetByIdAsync(id);
        if (receivable == null) return null;

        var dto = _mapper.Map<ReceivableDetailDto>(receivable);

        // 派生应收单号
        dto.ReceivableNo = ReceivableNoFormatter.Format(dto.Id);

        // 补全订单号与客户名称
        var order = await _orderRepository.GetByIdAsync(receivable.OrderId);
        if (order != null) dto.OrderNo = order.OrderNo;

        var member = await _memberRepository.GetByIdAsync(receivable.MemberId);
        if (member != null) dto.MemberName = member.Name;

        // 核销记录（按核销时间倒序，最近一次在前）
        var writeOffs = await _receivableRepository.GetWriteOffsAsync(id);
        dto.WriteOffs = writeOffs.Select(w => new WriteOffRecordDto
        {
            PaymentItemId = w.Item.Id,
            PaymentId = w.Item.PaymentId,
            PaymentNo = w.PaymentNo,
            OffsetAmount = w.Item.OffsetAmount,
            PaymentMethod = w.PaymentMethod,
            PaymentMethodName = PaymentMethods.GetName(w.PaymentMethod),
            PaymentDate = w.PaymentDate,
            CreateTime = w.Item.CreateTime
        }).ToList();

        return dto;
    }

    /// <summary>
    /// 【关键】订单发货 &amp; is_credit=1 时，自动生成应收账款。
    ///
    /// 规则：
    ///   1) 非信用订单（is_credit != 1）直接返回 0，不产生应收；
    ///   2) 幂等：同一订单已有应收记录则直接返回其ID，避免重复生成；
    ///   3) 到期日 = 订单日期(取支付时间，无则创建时间) + 客户账期天数(mem_member.account_period)；
    ///   4) 应收总额取订单实付金额 pay_amount；初始 paid=0、unpaid=total、状态=未结清(0)。
    /// </summary>
    public async Task<long> SyncFromOrderAsync(long orderId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
        {
            throw new InvalidOperationException($"订单 {orderId} 不存在，无法生成应收账款");
        }

        // 1) 仅信用订单产生应收
        if (order.IsCredit != 1) return 0;

        // 2) 幂等保护：同一订单只生成一条
        var exist = await _receivableRepository.GetByOrderIdAsync(orderId);
        if (exist != null) return exist.Id;

        // 3) 取客户账期天数（客户不存在则按 0 天 = 款到发货）
        var member = await _memberRepository.GetByIdAsync(order.BuyerId);
        var accountPeriod = member?.AccountPeriod ?? 0;

        // 订单日期：优先支付时间，其次创建时间
        var orderDate = (order.PayTime ?? order.CreateTime).Date;

        var receivable = new TrxReceivable
        {
            OrderId = order.Id,
            MemberId = order.BuyerId,
            TotalAmount = order.PayAmount,
            PaidAmount = 0,
            UnpaidAmount = order.PayAmount,
            OrderDate = orderDate,
            DueDate = orderDate.AddDays(accountPeriod),
            Status = ReceivableStatus.Unsettled,
            Remark = $"订单 {order.OrderNo} 发货自动生成"
        };

        await _receivableRepository.AddAsync(receivable);
        await _receivableRepository.SaveChangesAsync();

        return receivable.Id;
    }
}
