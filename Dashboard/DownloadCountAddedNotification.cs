using Dashboard.Database.Entities;

using MediatR;

namespace Dashboard;

public record DownloadCountAddedNotification(DownloadCount NewDownloadCount) : IEvent;
