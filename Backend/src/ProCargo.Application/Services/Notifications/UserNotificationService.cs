using Microsoft.Extensions.Caching.Memory;
using ProCargo.Application.Authorization;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Requests;
using ProCargo.Domain.Constants;

namespace ProCargo.Application.Services.Notifications;

public sealed class UserNotificationService : IUserNotificationService
{
    private readonly INotificationRepository _repository;
    private readonly AccessGuard _access;
    private readonly IAuditLogger _audit;
    private readonly IMemoryCache _cache;

    public UserNotificationService(INotificationRepository repository, AccessGuard access, IAuditLogger audit, IMemoryCache cache)
    {
        _repository = repository;
        _access = access;
        _audit = audit;
        _cache = cache;
    }

    public Task<PagedResult<NotificationDto>> GetMineAsync(NotificationSearchRequest request, CancellationToken cancellationToken) =>
        _repository.GetByUserAsync(_access.User.UserId, request, cancellationToken);

    public Task<int> GetUnreadCountAsync(CancellationToken cancellationToken) =>
        _repository.GetUnreadCountAsync(_access.User.UserId, cancellationToken);

    public Task MarkReadAsync(long? notificationId, CancellationToken cancellationToken) =>
        _repository.MarkReadAsync(_access.User.UserId, notificationId, cancellationToken);

    public Task<IReadOnlyList<NotificationTemplateDto>> GetTemplatesAsync(CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageNotificationTemplates);
        return _repository.GetAllTemplatesAsync(cancellationToken);
    }

    public async Task UpdateTemplateAsync(int templateId, UpdateNotificationTemplateRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageNotificationTemplates);
        var template = (await _repository.GetAllTemplatesAsync(cancellationToken)).FirstOrDefault(t => t.NotificationTemplateId == templateId)
                       ?? throw NotFoundException.For("NotificationTemplate", templateId);

        await _repository.UpdateTemplateAsync(templateId, request, _access.User.UserId, cancellationToken);
        NotificationService.InvalidateTemplates(_cache, template.TemplateCode);
        await _audit.LogAsync("NotificationTemplateUpdated", "NotificationTemplate", templateId,
            oldValue: new { template.Subject, template.Body, template.IsActive },
            newValue: new { request.Subject, request.Body, request.IsActive }, cancellationToken: cancellationToken);
    }
}
