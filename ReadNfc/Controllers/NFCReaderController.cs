using Microsoft.AspNetCore.Mvc;
using ReadNfc.Service;

namespace ReadNfc.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NFCReaderController : ControllerBase
    {
        private readonly NFCBackgroundService _nfcBackgroundService;

        public NFCReaderController(NFCBackgroundService nfcBackgroundService)
        {
            _nfcBackgroundService = nfcBackgroundService;
        }

        [HttpGet("getCardUID")]
        public IActionResult GetCardUID()
        {
            var uid = _nfcBackgroundService.GetCardUID();
            if (string.IsNullOrEmpty(uid))
            {
                return NotFound(new { message = "No card inserted." });
            }
            return Ok(new { CardUID = uid });
        }
    }
}