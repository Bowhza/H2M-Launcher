using Dashboard.Database.Entities;

using MediatR;

namespace Dashboard.Downloads;

public record DownloadCountAddedNotification(DownloadCount NewDownloadCount) : IEvent;
