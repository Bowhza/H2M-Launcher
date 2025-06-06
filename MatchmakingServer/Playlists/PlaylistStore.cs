﻿using JsonFlatFileDataStore;

namespace MatchmakingServer.Playlists;

public sealed class PlaylistStore
{
    private readonly DataStore _dataStore = new("./Data/playlists.json", keyProperty: "_id");

    /// <summary>
    /// Gets the number of playlists stored.
    /// </summary>
    public int PlaylistsCount => GetCollection().Count;

    /// <summary>
    /// Gets all stored playlists.
    /// </summary>
    public Task<ICollection<PlaylistDbo>> GetAllPlaylists()
    {
        return Task.FromResult<ICollection<PlaylistDbo>>(
            GetCollection()
                .AsQueryable()
                .ToList()
        );
    }

    /// <summary>
    /// Gets the playlist with the given <paramref name="id"/>, 
    /// or <see langword="null"/> if no such playlist exists.
    /// </summary>
    public Task<PlaylistDbo?> GetPlaylist(string id)
    {
        return Task.FromResult<PlaylistDbo?>(
            GetCollection()
                .AsQueryable()
                .FirstOrDefault(p => p.Id.Equals(id, StringComparison.InvariantCultureIgnoreCase))
        );
    }

    /// <summary>
    /// Replaces the playlist with the given <paramref name="playlist"/> id or creates a new one if not found.
    /// </summary>
    /// <param name="playlist">The playlist object to replace / create.</param>
    /// <returns>True, if successfully updated or inserted.</returns>
    public Task<bool> UpsertPlaylist(PlaylistDbo playlist)
    {
        return GetCollection()
            .ReplaceOneAsync(
                p => p.Id.Equals(playlist.Id, StringComparison.InvariantCultureIgnoreCase), 
                playlist, 
                upsert: true
             );
    }

    /// <summary>
    /// Removes the playlist with the given <paramref name="id"/>.
    /// </summary>
    /// <returns>True, if the playlist was successfully removed; false otherwise.</returns>
    public Task<bool> RemovePlaylist(string id)
    {
        return GetCollection()
            .DeleteOneAsync(p => p.Id.Equals(id, StringComparison.InvariantCultureIgnoreCase));
    }

    /// <summary>
    /// Seeds the store with all the given <paramref name="playlists"/>.
    /// </summary>
    /// <returns>True, if the operation was successful.</returns>
    public Task<bool> SeedPlaylists(IEnumerable<PlaylistDbo> playlists)
    {
        return Task.FromResult(GetCollection().InsertMany(playlists));
    }

    private IDocumentCollection<PlaylistDbo> GetCollection()
    {
        return _dataStore.GetCollection<PlaylistDbo>("playlists");
    }
}
