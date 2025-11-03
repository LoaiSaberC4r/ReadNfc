using PCSC;
using PCSC.Utils;

namespace ReadNfc.Service
{
    public class NFCService
    {
        private readonly string _readerName;
        private readonly int _waitMs;

        public NFCService(string readerName, int waitForCardMs = 3000)
        {
            _readerName = readerName;
            _waitMs = waitForCardMs;
        }

        public string ReadUID()
        {
            using var ctx = new SCardContext();
            ctx.Establish(SCardScope.System);

            // تِأكد إن القارئ موجود
            var readers = ctx.GetReaders();
            if (readers is null || readers.Length == 0)
                throw new InvalidOperationException("No NFC readers found.");

            // انتظر وجود كارت (اختياري لكنه مفيد)
            WaitForCardPresent(ctx, _readerName, _waitMs);

            using var reader = new SCardReader(ctx);

            // اتصل بالكارت - البروتوكولات المدعومة T=0/T=1
            var rc = reader.Connect(_readerName, SCardShareMode.Shared, SCardProtocol.T0 | SCardProtocol.T1);
            if (rc != SCardError.Success)
                throw new InvalidOperationException($"Connect failed: {SCardHelper.StringifyError(rc)}");

            // حدد PCI حسب البروتوكول المتفاوض عليه
            var active = reader.ActiveProtocol;
            var sendPci = active switch
            {
                SCardProtocol.T0 => SCardPCI.T0,
                SCardProtocol.T1 => SCardPCI.T1,
                SCardProtocol.Raw => SCardPCI.Raw,
                _ => throw new InvalidOperationException($"Active protocol is {active} (Unset).")
            };

            // APDU قياسي للحصول على UID على ACR122U
            var cmdGetUid = new byte[] { 0xFF, 0xCA, 0x00, 0x00, 0x00 };

            var recvPci = new SCardPCI();               // لا يهم محتواه، مطلوب للتوقيع
            var recv = new byte[256];

            rc = reader.Transmit(sendPci, cmdGetUid, recvPci, ref recv);
            if (rc != SCardError.Success)
                throw new InvalidOperationException($"Transmit failed: {SCardHelper.StringifyError(rc)}");

            // الاستجابة: [UID bytes ...][SW1][SW2]
            // نجاح عادةً SW1SW2 = 0x90 0x00
            if (recv.Length < 2) throw new InvalidOperationException("Invalid response.");
            var sw1 = recv[^2];
            var sw2 = recv[^1];
            if (!(sw1 == 0x90 && sw2 == 0x00))
                throw new InvalidOperationException($"Card returned status {sw1:X2}{sw2:X2}");

            var uidLen = recv.Length - 2;
            // بعض الإطارات تكون أكبر من اللازم: قص على أول uidLen الحقيقي
            // نحسب الطول الفعلي حتى قبل 0x90,0x00
            int actualLen = uidLen;
            while (actualLen > 0 && recv[actualLen - 1] == 0x00) actualLen--; // حماية بسيطة
            if (actualLen == 0) throw new InvalidOperationException("Empty UID.");

            var uidBytes = new byte[actualLen];
            Array.Copy(recv, 0, uidBytes, 0, actualLen);

            var uid = BitConverter.ToString(uidBytes).Replace("-", string.Empty);
            return uid;
        }

        private static void WaitForCardPresent(ISCardContext ctx, string readerName, int timeoutMs)
        {
            var state = new SCardReaderState
            {
                ReaderName = readerName,
                CurrentState = SCRState.Empty
            };

            var rc = ctx.GetStatusChange(timeoutMs, new[] { state });
            if (rc == SCardError.Timeout)
                throw new TimeoutException("No card present.");
            if (rc != SCardError.Success)
                throw new InvalidOperationException($"GetStatusChange failed: {SCardHelper.StringifyError(rc)}");
            // لو فيه كارت هيكون state.EventState فيه Present/Changed
        }
    }
}