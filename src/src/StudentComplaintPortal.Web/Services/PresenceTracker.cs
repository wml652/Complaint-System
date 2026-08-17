using System.Collections.Concurrent;
using System.Linq;

namespace StudentComplaintPortal.Web.Services;

public class PresenceTracker
{
    // UserId -> set of active SignalR ConnectionIds (multiple tabs/devices possible)
    private static readonly ConcurrentDictionary<string, HashSet<string>> OnlineUsers = new();
    private static readonly object LockObj = new();

    // Returns true if this is the user's FIRST connection (i.e. they just came online)
    public bool UserConnected(string userId, string connectionId)
    {
        lock (LockObj)
        {
            bool isNewlyOnline = false;
            if (!OnlineUsers.ContainsKey(userId))
            {
                OnlineUsers[userId] = new HashSet<string>();
                isNewlyOnline = true;
            }
            OnlineUsers[userId].Add(connectionId);
            return isNewlyOnline;
        }
    }

    // Returns true if this was the user's LAST connection (i.e. they just went offline)
    public bool UserDisconnected(string userId, string connectionId)
    {
        lock (LockObj)
        {
            if (!OnlineUsers.ContainsKey(userId)) return false;

            OnlineUsers[userId].Remove(connectionId);

            if (OnlineUsers[userId].Count == 0)
            {
                OnlineUsers.TryRemove(userId, out _);
                return true;
            }
            return false;
        }
    }
    public List<string> GetOnlineUserIds()
    {
        lock (LockObj)
        {
            return OnlineUsers.Keys.ToList();
        }
    }

    public bool IsOnline(string userId) => OnlineUsers.ContainsKey(userId);
}
