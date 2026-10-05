using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Domain.Enums;
using ProCargo.Infrastructure.Persistence;

namespace ProCargo.Infrastructure.Repositories;

internal sealed class DocumentRepository : IDocumentRepository
{
    private readonly StoredProcedureExecutor _sp;

    public DocumentRepository(StoredProcedureExecutor sp)
    {
        _sp = sp;
    }

    public async Task<long> CreateStoredFileAsync(StoredFileCommand c, CancellationToken cancellationToken) =>
        (await _sp.QuerySingleAsync<IdResult>($"""
            EXEC core.usp_StoredFile_Create @StorageKey={c.StorageKey}, @OriginalFileName={c.OriginalFileName}, @ContentType={c.ContentType},
                @SizeBytes={c.SizeBytes}, @Sha256Hash={c.Sha256Hash}, @UploadedBy={c.UploadedBy}
            """, cancellationToken)).Id;

    public Task<StoredFileDto?> GetStoredFileAsync(long storedFileId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<StoredFileDto>($"EXEC core.usp_StoredFile_GetById @StoredFileId={storedFileId}", cancellationToken);

    public async Task<long> CreateDocumentAsync(DocumentEntityType entityType, long entityId, int documentTypeId, long storedFileId,
        string? documentNumber, DateOnly? expiryDate, long createdBy, CancellationToken cancellationToken) =>
        (await _sp.QuerySingleAsync<IdResult>($"""
            EXEC core.usp_EntityDocument_Create @EntityType={entityType.ToString()}, @EntityId={entityId}, @DocumentTypeId={documentTypeId},
                @StoredFileId={storedFileId}, @DocumentNumber={documentNumber}, @ExpiryDate={expiryDate}, @CreatedBy={createdBy}
            """, cancellationToken)).Id;

    public Task<IReadOnlyList<DocumentDto>> GetDocumentsAsync(DocumentEntityType entityType, long entityId, CancellationToken cancellationToken) =>
        _sp.QueryAsync<DocumentDto>($"""
            EXEC core.usp_EntityDocument_Get @EntityType={entityType.ToString()}, @EntityId={entityId}, @DocumentId={(long?)null}
            """, cancellationToken);

    public Task<DocumentDto?> GetDocumentAsync(DocumentEntityType entityType, long entityId, long documentId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<DocumentDto>($"""
            EXEC core.usp_EntityDocument_Get @EntityType={entityType.ToString()}, @EntityId={entityId}, @DocumentId={documentId}
            """, cancellationToken);

    public Task SetVerificationAsync(DocumentEntityType entityType, long entityId, long documentId, VerificationStatus status, string? remarks,
        long verifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_EntityDocument_SetVerification @EntityType={entityType.ToString()}, @EntityId={entityId}, @DocumentId={documentId},
                @VerificationStatusId={(int)status}, @Remarks={remarks}, @VerifiedBy={verifiedBy}
            """, cancellationToken);

    public Task DeleteAsync(DocumentEntityType entityType, long entityId, long documentId, long modifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_EntityDocument_Delete @EntityType={entityType.ToString()}, @EntityId={entityId}, @DocumentId={documentId}, @ModifiedBy={modifiedBy}
            """, cancellationToken);
}
