using ProCargo.Application.Authorization;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Requests;
using ProCargo.Domain.Constants;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Services.Partners;

/// <summary>
/// KYC / compliance documents of customers, owners, drivers and vehicles.
/// The file and its metadata are stored in one database transaction (file row + document row); the blob is
/// written first, so a failed transaction can at worst leave an orphaned blob, never a row without a file.
/// </summary>
public sealed class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documents;
    private readonly IFileService _files;
    private readonly IFileStorage _storage;
    private readonly IDriverRepository _drivers;
    private readonly IVehicleRepository _vehicles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AccessGuard _access;
    private readonly IAuditLogger _audit;

    public DocumentService(IDocumentRepository documents, IFileService files, IFileStorage storage, IDriverRepository drivers,
        IVehicleRepository vehicles, IUnitOfWork unitOfWork, AccessGuard access, IAuditLogger audit)
    {
        _documents = documents;
        _files = files;
        _storage = storage;
        _drivers = drivers;
        _vehicles = vehicles;
        _unitOfWork = unitOfWork;
        _access = access;
        _audit = audit;
    }

    public async Task<IReadOnlyList<DocumentDto>> GetAsync(DocumentEntityType entityType, long entityId, CancellationToken cancellationToken)
    {
        await EnsureAccessAsync(entityType, entityId, write: false, cancellationToken);
        return await _documents.GetDocumentsAsync(entityType, entityId, cancellationToken);
    }

    public async Task<long> UploadAsync(DocumentEntityType entityType, long entityId, UploadDocumentRequest request, FileUpload file,
        CancellationToken cancellationToken)
    {
        await EnsureAccessAsync(entityType, entityId, write: true, cancellationToken);

        var documentId = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var storedFileId = await _files.StoreAsync(file, $"documents/{entityType.ToString().ToLowerInvariant()}", ct);
            return await _documents.CreateDocumentAsync(entityType, entityId, request.DocumentTypeId, storedFileId,
                request.DocumentNumber?.Trim().ToUpperInvariant(), request.ExpiryDate, _access.User.UserId, ct);
        }, cancellationToken);

        await _audit.LogAsync("DocumentUploaded", entityType.ToString(), entityId,
            newValue: new { DocumentId = documentId, request.DocumentTypeId, request.ExpiryDate }, cancellationToken: cancellationToken);
        return documentId;
    }

    public async Task<FileDownload> DownloadAsync(DocumentEntityType entityType, long entityId, long documentId, CancellationToken cancellationToken)
    {
        await EnsureAccessAsync(entityType, entityId, write: false, cancellationToken);
        var document = await _documents.GetDocumentAsync(entityType, entityId, documentId, cancellationToken)
                       ?? throw NotFoundException.For("Document", documentId);

        var stream = await _storage.OpenReadAsync(document.StorageKey, cancellationToken);
        return new FileDownload(stream, document.ContentType, document.OriginalFileName);
    }

    public async Task VerifyAsync(DocumentEntityType entityType, long entityId, long documentId, VerificationDecisionRequest request,
        CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(ApprovePermission(entityType));
        await _documents.SetVerificationAsync(entityType, entityId, documentId, request.Status, request.Remarks, _access.User.UserId, cancellationToken);
        await _audit.LogAsync("DocumentVerification", entityType.ToString(), entityId,
            newValue: new { DocumentId = documentId, Status = request.Status.ToString(), request.Remarks }, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(DocumentEntityType entityType, long entityId, long documentId, CancellationToken cancellationToken)
    {
        await EnsureAccessAsync(entityType, entityId, write: true, cancellationToken);
        await _documents.DeleteAsync(entityType, entityId, documentId, _access.User.UserId, cancellationToken);
        await _audit.LogAsync("DocumentDeleted", entityType.ToString(), entityId, newValue: new { DocumentId = documentId },
            cancellationToken: cancellationToken);
    }

    private async Task EnsureAccessAsync(DocumentEntityType entityType, long entityId, bool write, CancellationToken cancellationToken)
    {
        var user = _access.User;
        switch (entityType)
        {
            case DocumentEntityType.Customer:
                _access.EnsureCustomer(entityId, write ? Permissions.ManageCustomers : Permissions.ViewCustomers);
                break;

            case DocumentEntityType.Owner:
                _access.EnsureOwner(entityId, write ? Permissions.ManageOwners : Permissions.ViewOwners);
                break;

            case DocumentEntityType.Driver:
            {
                var driver = await _drivers.GetByIdAsync(entityId, cancellationToken) ?? throw NotFoundException.For("Driver", entityId);
                _access.EnsureDriver(driver.DriverId, driver.OwnerId, write ? Permissions.ManageDrivers : Permissions.ViewDrivers);
                break;
            }

            case DocumentEntityType.Vehicle:
            {
                var vehicle = await _vehicles.GetByIdAsync(entityId, cancellationToken) ?? throw NotFoundException.For("Vehicle", entityId);
                if (_access.IsStaffWith(write ? Permissions.ManageVehicles : Permissions.ViewVehicles)) break;
                if (user.OwnerId == vehicle.OwnerId) break;
                throw NotFoundException.For("Vehicle", entityId);
            }

            default:
                throw new RequestValidationException("entityType", "Unsupported document owner type.");
        }
    }

    private static string ApprovePermission(DocumentEntityType entityType) => entityType switch
    {
        DocumentEntityType.Customer => Permissions.ManageCustomers,
        DocumentEntityType.Owner => Permissions.ApproveOwners,
        DocumentEntityType.Driver => Permissions.ApproveDrivers,
        DocumentEntityType.Vehicle => Permissions.ApproveVehicles,
        _ => Permissions.ManageMasterData
    };
}
