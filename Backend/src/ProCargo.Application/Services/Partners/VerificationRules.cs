using ProCargo.Application.DTOs;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Services.Partners;

/// <summary>
/// KYC rule shared by owners, drivers and vehicles: a record can only be marked Verified when every
/// mandatory document type for it has been uploaded, is verified and has not expired.
/// </summary>
public sealed class VerificationRules
{
    private readonly IDocumentRepository _documents;
    private readonly IMasterDataRepository _masterData;
    private readonly TimeProvider _clock;

    public VerificationRules(IDocumentRepository documents, IMasterDataRepository masterData, TimeProvider clock)
    {
        _documents = documents;
        _masterData = masterData;
        _clock = clock;
    }

    public async Task EnsureMandatoryDocumentsVerifiedAsync(DocumentEntityType entityType, long entityId, CancellationToken cancellationToken)
    {
        var mandatory = (await _masterData.GetDocumentTypesAsync(entityType.ToString(), false, cancellationToken))
            .Where(t => t.IsMandatory)
            .ToList();
        if (mandatory.Count == 0) return;

        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var documents = await _documents.GetDocumentsAsync(entityType, entityId, cancellationToken);

        var missing = mandatory
            .Where(type => !documents.Any(d => d.DocumentTypeId == type.DocumentTypeId
                                               && d.VerificationStatusId == (int)VerificationStatus.Verified
                                               && (d.ExpiryDate is null || d.ExpiryDate >= today)))
            .Select(type => type.Name)
            .ToList();

        if (missing.Count > 0)
        {
            throw new BusinessRuleException("MANDATORY_DOCUMENTS_MISSING",
                $"Verify these documents first: {string.Join(", ", missing)}.");
        }
    }

    public static string Describe(VerificationStatus status) => status switch
    {
        VerificationStatus.UnderReview => "Under Review",
        _ => status.ToString()
    };

    public static IReadOnlyDictionary<string, string> NotificationData(string subject, VerificationDecisionRequestLike decision) =>
        new Dictionary<string, string>
        {
            ["Subject"] = subject,
            ["Status"] = Describe(decision.Status),
            ["Remarks"] = decision.Remarks ?? string.Empty
        };
}

/// <summary>Minimal view of a verification decision used for notifications.</summary>
public readonly record struct VerificationDecisionRequestLike(VerificationStatus Status, string? Remarks);

internal static class DocumentExtensions
{
    public static bool IsVerifiedOn(this DocumentDto document, DateOnly date) =>
        document.VerificationStatusId == (int)VerificationStatus.Verified && (document.ExpiryDate is null || document.ExpiryDate >= date);
}
