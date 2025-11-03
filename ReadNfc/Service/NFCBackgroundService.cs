using PCSC;
using PCSC.Monitoring;
using PCSC.Utils;

namespace ReadNfc.Service
{
    public class NFCBackgroundService : BackgroundService
    {
        private readonly IContextFactory _contextFactory;
        private readonly string _readerName;
        private string _cardUID;

        public NFCBackgroundService(IContextFactory contextFactory)
        {
            _contextFactory = contextFactory;
            _readerName = GetReaderName();  // هذه هي الطريقة لاختيار قارئ NFC المتصل
            _cardUID = string.Empty;
        }

        public string GetCardUID() => _cardUID;

        private string GetReaderName()
        {
            using var ctx = _contextFactory.Establish(SCardScope.System);
            var readers = ctx.GetReaders();
            if (readers == null || readers.Length == 0)
                throw new InvalidOperationException("No NFC readers found.");

            return readers[0];  // استخدام أول قارئ موجود
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var monitor = new SCardMonitor(_contextFactory, SCardScope.System);
            monitor.CardInserted += (sender, e) => OnCardInserted();
            monitor.CardRemoved += (sender, e) => OnCardRemoved();
            monitor.MonitorException += (sender, e) => Console.WriteLine($"Monitor error: {e.Message}");

            monitor.Start(_readerName);
            await Task.Delay(-1, stoppingToken);  // الخدمة ستعمل إلى أن يتم إيقافها
        }

        private void OnCardInserted()
        {
            Console.WriteLine("Card inserted.");
            _cardUID = ReadUID();
        }

        private void OnCardRemoved()
        {
            Console.WriteLine("Card removed.");
            _cardUID = string.Empty;
        }

        private string ReadUID()
        {
            using var ctx = _contextFactory.Establish(SCardScope.System);
            using var reader = new SCardReader(ctx);

            var rc = reader.Connect(_readerName, SCardShareMode.Shared, SCardProtocol.Any);
            if (rc != SCardError.Success)
                throw new InvalidOperationException($"Connect failed: {SCardHelper.StringifyError(rc)}");

            var send = new byte[] { 0xFF, 0xCA, 0x00, 0x00, 0x00 };
            var recv = new byte[256];
            var sendPci = SCardPCI.GetPci(reader.ActiveProtocol);
            var recvPci = new SCardPCI();

            rc = reader.Transmit(sendPci, send, recvPci, ref recv);
            if (rc != SCardError.Success)
                throw new InvalidOperationException($"Transmit failed: {SCardHelper.StringifyError(rc)}");

            var sw1 = recv[^2];
            var sw2 = recv[^1];
            if (sw1 == 0x90 && sw2 == 0x00)
            {
                int dataLen = recv.Length - 2;
                dataLen = Array.FindLastIndex(recv, recv.Length - 3, recv.Length - 2, b => b != 0x00);
                if (dataLen < 0) dataLen = 0;

                byte[] data = recv[..(recv.Length - 2)];
                int realLen = Array.FindLastIndex(data, b => b != 0x00) + 1;
                byte[] uid = data[..realLen];

                var uidHex = BitConverter.ToString(uid).Replace("-", "");
                return uidHex;
            }
            else
            {
                throw new InvalidOperationException($"APDU failed SW={sw1:X2}{sw2:X2}");
            }
        }
    }
}