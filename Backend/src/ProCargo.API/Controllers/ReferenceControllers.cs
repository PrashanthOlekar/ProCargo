using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProCargo.API.Authorization;
using ProCargo.API.Extensions;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Requests;
using ProCargo.Application.Responses;
using ProCargo.Domain.Constants;

namespace ProCargo.API.Controllers;

/// <summary>Reference data (public read) and its administration (ManageMasterData).</summary>
[Route("api/v1/master-data")]
public sealed class MasterDataController : ApiControllerBase
{
    private readonly IMasterDataService _masterData;

    public MasterDataController(IMasterDataService masterData)
    {
        _masterData = masterData;
    }

    /// <summary>Everything the portals need to render forms: lookups, states, cities, vehicle and goods types.</summary>
    [HttpGet("reference")]
    [AllowAnonymous]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public Task<ReferenceDataResponse> GetReference(CancellationToken cancellationToken) => _masterData.GetReferenceDataAsync(cancellationToken);

    [HttpGet("states")]
    [AllowAnonymous]
    public Task<IReadOnlyList<StateDto>> GetStates([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        _masterData.GetStatesAsync(includeInactive && User.IsOperations(), cancellationToken);

    [HttpPost("states")]
    [HasPermission(Permissions.ManageMasterData)]
    public async Task<CreatedResponse> CreateState(SaveStateRequest request, CancellationToken cancellationToken) =>
        new(await _masterData.SaveStateAsync(null, request, cancellationToken), null);

    [HttpPut("states/{id:int}")]
    [HasPermission(Permissions.ManageMasterData)]
    public async Task<IActionResult> UpdateState(int id, SaveStateRequest request, CancellationToken cancellationToken)
    {
        await _masterData.SaveStateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("cities")]
    [AllowAnonymous]
    public Task<IReadOnlyList<CityDto>> GetCities([FromQuery] int? stateId, [FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        _masterData.GetCitiesAsync(stateId, includeInactive && User.IsOperations(), cancellationToken);

    [HttpPost("cities")]
    [HasPermission(Permissions.ManageMasterData)]
    public async Task<CreatedResponse> CreateCity(SaveCityRequest request, CancellationToken cancellationToken) =>
        new(await _masterData.SaveCityAsync(null, request, cancellationToken), null);

    [HttpPut("cities/{id:int}")]
    [HasPermission(Permissions.ManageMasterData)]
    public async Task<IActionResult> UpdateCity(int id, SaveCityRequest request, CancellationToken cancellationToken)
    {
        await _masterData.SaveCityAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("vehicle-types")]
    [AllowAnonymous]
    public Task<IReadOnlyList<VehicleTypeDto>> GetVehicleTypes([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        _masterData.GetVehicleTypesAsync(includeInactive && User.IsOperations(), cancellationToken);

    [HttpPost("vehicle-types")]
    [HasPermission(Permissions.ManageMasterData)]
    public async Task<CreatedResponse> CreateVehicleType(SaveVehicleTypeRequest request, CancellationToken cancellationToken) =>
        new(await _masterData.SaveVehicleTypeAsync(null, request, cancellationToken), null);

    [HttpPut("vehicle-types/{id:int}")]
    [HasPermission(Permissions.ManageMasterData)]
    public async Task<IActionResult> UpdateVehicleType(int id, SaveVehicleTypeRequest request, CancellationToken cancellationToken)
    {
        await _masterData.SaveVehicleTypeAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("goods-types")]
    [AllowAnonymous]
    public Task<IReadOnlyList<GoodsTypeDto>> GetGoodsTypes([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        _masterData.GetGoodsTypesAsync(includeInactive && User.IsOperations(), cancellationToken);

    [HttpPost("goods-types")]
    [HasPermission(Permissions.ManageMasterData)]
    public async Task<CreatedResponse> CreateGoodsType(SaveGoodsTypeRequest request, CancellationToken cancellationToken) =>
        new(await _masterData.SaveGoodsTypeAsync(null, request, cancellationToken), null);

    [HttpPut("goods-types/{id:int}")]
    [HasPermission(Permissions.ManageMasterData)]
    public async Task<IActionResult> UpdateGoodsType(int id, SaveGoodsTypeRequest request, CancellationToken cancellationToken)
    {
        await _masterData.SaveGoodsTypeAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("document-types")]
    [Authorize]
    public Task<IReadOnlyList<DocumentTypeDto>> GetDocumentTypes([FromQuery] string? appliesTo, [FromQuery] bool includeInactive,
        CancellationToken cancellationToken) =>
        _masterData.GetDocumentTypesAsync(appliesTo, includeInactive && User.IsOperations(), cancellationToken);

    [HttpPost("document-types")]
    [HasPermission(Permissions.ManageMasterData)]
    public async Task<CreatedResponse> CreateDocumentType(SaveDocumentTypeRequest request, CancellationToken cancellationToken) =>
        new(await _masterData.SaveDocumentTypeAsync(null, request, cancellationToken), null);

    [HttpPut("document-types/{id:int}")]
    [HasPermission(Permissions.ManageMasterData)]
    public async Task<IActionResult> UpdateDocumentType(int id, SaveDocumentTypeRequest request, CancellationToken cancellationToken)
    {
        await _masterData.SaveDocumentTypeAsync(id, request, cancellationToken);
        return NoContent();
    }
}

[Route("api/v1/settings")]
public sealed class SettingsController : ApiControllerBase
{
    private readonly IMasterDataService _masterData;

    public SettingsController(IMasterDataService masterData)
    {
        _masterData = masterData;
    }

    [HttpGet]
    [HasPermission(Permissions.ManageSystemSettings)]
    public Task<IReadOnlyList<SystemSettingDto>> Get(CancellationToken cancellationToken) => _masterData.GetSystemSettingsAsync(cancellationToken);

    [HttpPut("{key}")]
    [HasPermission(Permissions.ManageSystemSettings)]
    public async Task<IActionResult> Update(string key, UpdateSystemSettingRequest request, CancellationToken cancellationToken)
    {
        await _masterData.UpdateSystemSettingAsync(key, request, cancellationToken);
        return NoContent();
    }
}

/// <summary>Price estimate (public) and pricing configuration (ManagePricing).</summary>
[Route("api/v1/pricing")]
public sealed class PricingController : ApiControllerBase
{
    private readonly IPricingService _pricing;

    public PricingController(IPricingService pricing)
    {
        _pricing = pricing;
    }

    /// <summary>Indicative price for the public website. The final price is set in the quotation.</summary>
    [HttpPost("estimate")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Public)]
    public Task<PriceEstimateResponse> Estimate(PriceEstimateRequest request, CancellationToken cancellationToken) =>
        _pricing.EstimateAsync(request, cancellationToken);

    [HttpGet("configuration")]
    [HasPermission(Permissions.ManagePricing)]
    public Task<PricingConfigurationResponse> GetConfiguration(CancellationToken cancellationToken) => _pricing.GetConfigurationAsync(cancellationToken);

    [HttpPost("vehicle-rates")]
    [HasPermission(Permissions.ManagePricing)]
    public async Task<CreatedResponse> CreateVehicleRate(SaveVehiclePricingRequest request, CancellationToken cancellationToken) =>
        new(await _pricing.SaveVehiclePricingAsync(null, request, cancellationToken), null);

    [HttpPut("vehicle-rates/{id:int}")]
    [HasPermission(Permissions.ManagePricing)]
    public async Task<IActionResult> UpdateVehicleRate(int id, SaveVehiclePricingRequest request, CancellationToken cancellationToken)
    {
        await _pricing.SaveVehiclePricingAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("distance-slabs")]
    [HasPermission(Permissions.ManagePricing)]
    public async Task<CreatedResponse> CreateDistanceSlab(SaveDistancePricingRequest request, CancellationToken cancellationToken) =>
        new(await _pricing.SaveDistancePricingAsync(null, request, cancellationToken), null);

    [HttpPut("distance-slabs/{id:int}")]
    [HasPermission(Permissions.ManagePricing)]
    public async Task<IActionResult> UpdateDistanceSlab(int id, SaveDistancePricingRequest request, CancellationToken cancellationToken)
    {
        await _pricing.SaveDistancePricingAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("additional-charges")]
    [HasPermission(Permissions.ManagePricing)]
    public async Task<CreatedResponse> CreateAdditionalCharge(SaveAdditionalChargeRequest request, CancellationToken cancellationToken) =>
        new(await _pricing.SaveAdditionalChargeAsync(null, request, cancellationToken), null);

    [HttpPut("additional-charges/{id:int}")]
    [HasPermission(Permissions.ManagePricing)]
    public async Task<IActionResult> UpdateAdditionalCharge(int id, SaveAdditionalChargeRequest request, CancellationToken cancellationToken)
    {
        await _pricing.SaveAdditionalChargeAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("rules")]
    [HasPermission(Permissions.ManagePricing)]
    public async Task<CreatedResponse> CreateRule(SavePricingRuleRequest request, CancellationToken cancellationToken) =>
        new(await _pricing.SavePricingRuleAsync(null, request, cancellationToken), null);

    [HttpPut("rules/{id:int}")]
    [HasPermission(Permissions.ManagePricing)]
    public async Task<IActionResult> UpdateRule(int id, SavePricingRuleRequest request, CancellationToken cancellationToken)
    {
        await _pricing.SavePricingRuleAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("tax-rates")]
    [HasPermission(Permissions.ManagePricing)]
    public async Task<CreatedResponse> CreateTaxRate(SaveTaxRateRequest request, CancellationToken cancellationToken) =>
        new(await _pricing.SaveTaxRateAsync(null, request, cancellationToken), null);

    [HttpPut("tax-rates/{id:int}")]
    [HasPermission(Permissions.ManagePricing)]
    public async Task<IActionResult> UpdateTaxRate(int id, SaveTaxRateRequest request, CancellationToken cancellationToken)
    {
        await _pricing.SaveTaxRateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("commission-rules")]
    [HasPermission(Permissions.ManagePricing)]
    public async Task<CreatedResponse> CreateCommissionRule(SaveCommissionRuleRequest request, CancellationToken cancellationToken) =>
        new(await _pricing.SaveCommissionRuleAsync(null, request, cancellationToken), null);

    [HttpPut("commission-rules/{id:int}")]
    [HasPermission(Permissions.ManagePricing)]
    public async Task<IActionResult> UpdateCommissionRule(int id, SaveCommissionRuleRequest request, CancellationToken cancellationToken)
    {
        await _pricing.SaveCommissionRuleAsync(id, request, cancellationToken);
        return NoContent();
    }
}

/// <summary>Public website endpoints that need no account.</summary>
[Route("api/v1/public")]
[AllowAnonymous]
public sealed class PublicController : ApiControllerBase
{
    private readonly ISupportService _support;

    public PublicController(ISupportService support)
    {
        _support = support;
    }

    [HttpPost("contact")]
    [EnableRateLimiting(RateLimitPolicies.Public)]
    public async Task<ActionResult<MessageResponse>> Contact(ContactEnquiryRequest request, CancellationToken cancellationToken)
    {
        await _support.SubmitContactEnquiryAsync(request, cancellationToken);
        return Ok(MessageResponse.Ok("Thank you. Our team will get back to you shortly."));
    }
}
