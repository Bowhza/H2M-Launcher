﻿using System.Diagnostics;

using H2MLauncher.Core.Party;

namespace MatchmakingServer.Parties;

public class Party : IParty
{
    private Player _leader;
    private readonly HashSet<Player> _members = [];
    private readonly Dictionary<string, DateTime> _invites = [];
    private bool _isClosed;

    public string Id { get; init; } = Guid.NewGuid().ToString();

    public PartyPrivacy Privacy { get; set; } = PartyPrivacy.Open;

    public bool IsClosed => _isClosed;
    public Player Leader => _leader;
    public IReadOnlySet<Player> Members => _members;
    public IReadOnlyDictionary<string, DateTime> Invites => _invites;

    public IEnumerable<string> ValidInvites => Invites.Where(i => DateTime.UtcNow < i.Value).Select(i => i.Key);

    public Party(Player leader)
    {
        _leader = leader;
    }

    public void AddPlayer(Player player)
    {
        ValidateClosed();

        lock (_members)
        {
            ValidateClosed();
            if (player.Party is not null)
            {
                throw new ArgumentException("Cannot add player to party. Player is already in a party.", nameof(player));
            }

            if (!_members.Add(player))
            {
                throw new ArgumentException("Cannot add player to party. Player is already in this party.", nameof(player));
            }

            // remove invite if player joins (consistency)
            _invites.Remove(player.Id);

            player.Party = this;
        }
    }

    private void ValidateClosed()
    {
        if (_isClosed)
        {
            throw new InvalidOperationException("Party is closed");
        }
    }

    public bool RemovePlayer(Player player)
    {
        ValidateClosed();

        lock (_members)
        {
            ValidateClosed();

            if (_members.Remove(player))
            {
                player.Party = null;
                return true;
            }

            return false;
        }
    }

    public IReadOnlyList<Player> CloseParty()
    {
        ValidateClosed();

        List<Player> removedPlayers = [];

        lock (_members)
        {
            ValidateClosed();

            _isClosed = true;

            foreach (Player member in _members)
            {
                member.Party = null;
                removedPlayers.Add(member);
            }

            _members.Clear();
        }

        return removedPlayers;
    }

    public Player ChangeLeader(Player player)
    {
        ValidateClosed();

        lock (_members)
        {
            ValidateClosed();

            if (_leader == player)
            {
                throw new ArgumentException("Cannot change leader. Player is already the leader.", nameof(player));
            }

            if (!_members.Contains(player))
            {
                throw new ArgumentException("Cannot change leader. Player is not in the party.", nameof(player));
            }

            Player oldLeader = _leader;
            _leader = player;

            return oldLeader;
        }
    }

    public void AddInvite(Player player, DateTime expirationTime)
    {
        ValidateClosed();

        lock (_members)
        {
            ValidateClosed();

            if (_members.Contains(player))
            {
                throw new ArgumentException("Cannot invite. Player is already in the party.", nameof(player));
            }

            _invites[player.Id] = expirationTime;
        }
    }

    public void RemoveInvite(string playerId)
    {
        ValidateClosed();

        lock (_members)
        {
            ValidateClosed();

            _invites.Remove(playerId);
        }
    }

    public bool IsValidInvite(string playerId)
    {
        if (Invites.TryGetValue(playerId, out DateTime expirationTime))
        {
            return DateTime.UtcNow < expirationTime;
        }
        return false;
    }
}
