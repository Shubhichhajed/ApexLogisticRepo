using ApexPost.Web.Attributes;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApexPost.Web.Controllers;

[AllowAnonymous]
[RequireApiKey]
[Route("integration")]
public sealed class IntegrationController : Controller
{
    [HttpGet("parcel-status")]
    [EnableRateLimiting("partner")]
    public IActionResult ParcelStatus([FromQuery] string trackingNumber = "APX-12345")
    {
        var response = new ParcelStatusResponse(trackingNumber, "In transit", "ApexPost Courier", "Z3");
        return Json(response);
    }
}
