using System.Threading.Channels;

namespace Deepfake_analysis.Background
{
    /// <summary>
    /// Очередь номеров анализов, которые ждут расчёта. Запрос кладёт номер и сразу отвечает,
    /// а фоновая служба <see cref="AnalysisWorker"/> забирает номера по одному.
    /// Channel — потокобезопасная очередь из .NET: писать и читать можно из разных потоков.
    /// </summary>
    public sealed class AnalysisQueue
    {
        private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

        public void Enqueue(int analysisId) => _channel.Writer.TryWrite(analysisId);

        /// <summary>Отдаёт номера по мере поступления; если очередь пуста — ждёт.</summary>
        public IAsyncEnumerable<int> ReadAllAsync(CancellationToken cancellationToken) =>
            _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
