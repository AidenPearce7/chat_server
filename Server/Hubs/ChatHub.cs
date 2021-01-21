using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
namespace Server.Hubs
{
    static class UserManagement
    {
        class User
        {
            public string ConnId { get; private set; }
            public string Username { get; set; }

            public User(string connId)
            {
                ConnId = connId;
                Username = connId;
            }
        }
        private static List<User> _users = new List<User>();

        public class UsernameConflictException : Exception
        {
            
        }

        public class UserDoesntExistException : Exception
        {
            
        }
        public static void Add(string connectionId)
        {
            _users.Add(new User(connectionId));
        }
        public static void Remove(string connectionId)
        {
            _users.Remove(_users.Find(user => user.ConnId.Equals(connectionId)));
        }
        public static void ChangeUsername(string connectionId, string username)
        {
            if (_users.Find(user => user.Username.Equals(username)) != null)
            {
                throw new UsernameConflictException();
                //Cannot change to that. Username ambiguous
            }
            _users.Find(user => user.ConnId.Equals(connectionId)).Username = username;
        }

        public static string GetIdFromUsername(string username)
        {
            var lookedUp = _users.Find(user => user.Username.Equals(username));
            if ( lookedUp != null)
            {
                return lookedUp.ConnId;
            }
            throw new UserDoesntExistException();
        }

        public static string GetUsername(string connectionId)
        {
            return _users.Find(user => user.ConnId.Equals(connectionId)).Username;
        }
    }
    public class ChatHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            UserManagement.Add(Context.ConnectionId);
            for (int i = 1; i < 5; i++)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Room {i}");
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            UserManagement.Remove(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(string room,string msg)
        {
            await Clients.Group(room).SendAsync("ReceiveMessage",room,UserManagement.GetUsername(Context.ConnectionId),msg);
        }

        public async Task LeaveGroup(string room)
        {
            if (room.StartsWith("Room"))
            {
                await Clients.Caller.SendAsync("CannotLeaveError");
            }
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, room);
            await Clients.Caller.SendAsync("GroupLeft",room);
        }

        public async Task OpenDirectMessage(string username)
        {
            try
            {
                string uid = UserManagement.GetIdFromUsername(username);
                if (uid.Equals(Context.ConnectionId))
                {
                    await Clients.Caller.SendAsync("SameUserError");
                    return;
                }
                string groupname = Context.ConnectionId + uid;
                string[] v = {Context.ConnectionId, uid};
                await Groups.AddToGroupAsync(Context.ConnectionId, groupname);
                await Groups.AddToGroupAsync(uid, groupname);
                await Clients.Clients(v).SendAsync("UpdateGroups", groupname);
            }
            catch (UserManagement.UserDoesntExistException e)
            {
                await Clients.Caller.SendAsync("UserDoesntExistError");
            }
        }

        public async Task ChangeUsername(string username)
        {
            try
            {
                string oldUsername = UserManagement.GetUsername(Context.ConnectionId);
                UserManagement.ChangeUsername(Context.ConnectionId, username);
                await Clients.Caller.SendAsync("UsernameChanged",username);
            }
            catch (UserManagement.UsernameConflictException e)
            {
                await Clients.Caller.SendAsync("UsernameExistsError");
            }
        }

        
    }

}
