using Dashboard.Database.Entities;

using MediatR;

namespace Dashboard.DownloadCount;

public record DownloadCountAddedNotification(DownloadCount NewDownloadCount) : IEvent;
