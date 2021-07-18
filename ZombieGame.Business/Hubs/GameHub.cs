﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using ZombieGame.Business.DTOs.Request;
using ZombieGame.Business.DTOs.Response;
using ZombieGame.Business.Hubs.HubModels;
using ZombieGame.Business.Utilities.Mapper;

namespace ZombieGame.Business.Hubs
{
    public class GameHub : Hub
    {
        private readonly IMapper _mapper;

        private static readonly Dictionary<string, string> PlayerRoomMap = new();

        private static readonly Dictionary<string, GameRoom> GameRooms = new();

        private static int Tick = 60;

        private static IHubContext<GameHub> HubContext;

        private static Timer Internal;

        public GameHub(IHubContext<GameHub> hubContext, IMapper mapper)
        {
            _mapper = mapper;
            HubContext = hubContext;
        }

        private static void GameLoop(object? state)
        {
            foreach (var gameRoom in GameRooms)
            {
                foreach (var player in gameRoom.Value.Players.Values.Where(p => p.Towards != Towards.NOWHERE))
                {
                    var posX = player.PosX;
                    var posY = player.PosY;
                    
                    switch (player.Towards)
                    {
                        case Towards.UP:
                            player.PosY -= gameRoom.Value.MoveSpeed;
                            break;
                        case Towards.DOWN:
                            player.PosY += gameRoom.Value.MoveSpeed;
                            break;
                        case Towards.RIGHT:
                            player.PosX += gameRoom.Value.MoveSpeed;
                            break;
                        case Towards.LEFT:
                            player.PosX -= gameRoom.Value.MoveSpeed;
                            break;
                    }

                    if (player.PosX > gameRoom.Value.SizeX || player.PosX < 0 || player.PosY > gameRoom.Value.SizeY ||
                        player.PosY < 0)
                    {
                        player.PosX = posX;
                        player.PosY = posY;
                    }
                }

                HubContext.Clients.Group(gameRoom.Key).SendAsync("update", gameRoom.Value.Players.Values);
            }
        }

        public async Task SendMove(MoveDto move)
        {
            try
            {
                var player = GameRooms[PlayerRoomMap[Context.ConnectionId]].Players[Context.ConnectionId];
                if (move.KeyState)
                {
                    player.Towards = move.Towards;
                }
                else if (player.Towards == move.Towards)
                {
                    player.Towards = Towards.NOWHERE;
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("onError", "Error : " + ex.Message);
            }
        }

        public async Task JoinGame(JoinDto joinDto)
        {
            try
            {
                var player = new Player(joinDto.Name);

                PlayerRoomMap[Context.ConnectionId] = joinDto.RoomId;

                var gameRoom = GameRooms[joinDto.RoomId];

                if (gameRoom.CurrentPlayerCount < gameRoom.PlayerCount)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, joinDto.RoomId);
                    gameRoom.Players[Context.ConnectionId] = player;
                    gameRoom.CurrentPlayerCount++;
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("onError", "You failed to join game room" + ex.Message);
            }
        }

        public async Task Leave(string roomId)
        {
            GameRooms[PlayerRoomMap[Context.ConnectionId]].Players.Remove(Context.ConnectionId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        }

        public IEnumerable<RoomOnDashboardDto> GetGameRooms()
        {
            //Init rooms if not exists.
            if (GameRooms.Values.Count == 0)
            {
                for (var i = 1; i <= 5; ++i)
                {
                    var gameRoom = new GameRoom($"Game Room {i}", i*5 , i*5);
                    GameRooms[gameRoom.Id] = gameRoom;
                }

                Internal = new Timer(GameLoop, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(1000 / Tick));
            }
            return _mapper.Map<List<GameRoom>, List<RoomOnDashboardDto>>(GameRooms.Values.ToList());
        }

        public override Task OnDisconnectedAsync(Exception ex)
        {
            if (PlayerRoomMap.ContainsKey(Context.ConnectionId) && GameRooms[PlayerRoomMap[Context.ConnectionId]].Players.ContainsKey(Context.ConnectionId))
            {
                GameRooms[PlayerRoomMap[Context.ConnectionId]].Players.Remove(Context.ConnectionId);
                GameRooms[PlayerRoomMap[Context.ConnectionId]].CurrentPlayerCount--;
                PlayerRoomMap.Remove(Context.ConnectionId);
            }

            return base.OnDisconnectedAsync(ex);
        }

        private string GetDevice()
        {
            var device = Context.GetHttpContext().Request.Headers["Device"].ToString();
            if (!string.IsNullOrEmpty(device) && (device.Equals("Desktop") || device.Equals("Mobile"))) return device;
            return "Web";
        }
    }
}