using System.Threading.Channels;
using Jsd.Api.Entities;

namespace Jsd.Api.Services;

/// <summary>
/// 操作日志异步写入队列（Channel 生产者/消费者）。
/// 【避坑·文档 6.1】日志写入绝不阻塞主流程：Filter 只负责把日志实体丢进 Channel，
/// 由 OperationLogBackgroundService 在后台逐条落库；队列满了就丢这条日志并打警告，
/// 保业务、舍日志（日志是审计辅助，不能反过来拖垮业务接口）。
/// </summary>
public class OperationLogWriter
{
    private readonly Channel<SysOperationLog> _channel;

    public OperationLogWriter()
    {
        // 有界队列：容量 10000，满了丢弃最新这条（TryWrite 返回 false）
        _channel = Channel.CreateBounded<SysOperationLog>(new BoundedChannelOptions(10000)
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <summary>投递一条操作日志（非阻塞，立即返回）</summary>
    public bool TryEnqueue(SysOperationLog log)
    {
        return _channel.Writer.TryWrite(log);
    }

    /// <summary>后台服务取读取器</summary>
    public ChannelReader<SysOperationLog> Reader => _channel.Reader;
}
