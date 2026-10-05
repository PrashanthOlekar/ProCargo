namespace ProCargo.Domain.Constants;

/// <summary>Codes of quotation / invoice charge lines and of configurable additional charges.</summary>
public static class ChargeCodes
{
    public const string Base = "BASE";
    public const string Distance = "DISTANCE";
    public const string Weight = "WEIGHT";
    public const string Loading = "LOADING";
    public const string Unloading = "UNLOADING";
    public const string Waiting = "WAITING";
    public const string Toll = "TOLL";
    public const string Night = "NIGHT";
    public const string SpecialHandling = "SPECIAL_HANDLING";
    public const string MinimumFareTopUp = "MIN_FARE";
    public const string RuleAdjustment = "RULE_ADJUSTMENT";
    public const string ManualAdjustment = "ADJUSTMENT";
    public const string Discount = "DISCOUNT";

    /// <summary>Tax code used for goods transport in fin.TaxRate.</summary>
    public const string GoodsTransportTax = "GST_GTA";
}
