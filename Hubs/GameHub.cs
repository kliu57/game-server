using Microsoft.AspNetCore.SignalR;

namespace GameServer.Hubs;

public class GameHub : Hub
{
    private static readonly List<Player> WaitingPlayers = new();
    private static readonly Dictionary<string, Game> ActiveGames = new();

    public async Task JoinQueue(string playerName)
    {
        var newPlayer = new Player(Context.ConnectionId, playerName);
        
        if (WaitingPlayers.Count > 0)
        {
            var opponent = WaitingPlayers[0];
            WaitingPlayers.RemoveAt(0);
            
            var game = new Game(newPlayer, opponent);
            ActiveGames[newPlayer.ConnectionId] = game;
            ActiveGames[opponent.ConnectionId] = game;
            
            await Clients.Client(opponent.ConnectionId).SendAsync("OpponentFound");
            await Clients.Client(newPlayer.ConnectionId).SendAsync("OpponentFound");
        }
        else
        {
            WaitingPlayers.Add(newPlayer);
        }
    }

    public async Task MakeChoice(string choice)
    {
        if (ActiveGames.TryGetValue(Context.ConnectionId, out var game))
        {
            game.MakeChoice(Context.ConnectionId, choice);
            
            if (game.IsBothPlayersChosen())
            {
                var player1Result = DetermineResult(game.Player1.Choice!, game.Player2.Choice!);
                var player2Result = DetermineResult(game.Player2.Choice!, game.Player1.Choice!);
                
                await Clients.Client(game.Player1.ConnectionId).SendAsync("GameResult", new
                {
                    playerChoice = game.Player1.Choice,
                    opponentChoice = game.Player2.Choice,
                    result = player1Result
                });
                
                await Clients.Client(game.Player2.ConnectionId).SendAsync("GameResult", new
                {
                    playerChoice = game.Player2.Choice,
                    opponentChoice = game.Player1.Choice,
                    result = player2Result
                });
                
                ActiveGames.Remove(game.Player1.ConnectionId);
                ActiveGames.Remove(game.Player2.ConnectionId);
            }
        }
    }

    private string DetermineResult(string playerChoice, string opponentChoice)
    {
        if (playerChoice == opponentChoice) return "draw";
        
        return (playerChoice, opponentChoice) switch
        {
            ("rock", "scissors") => "win",
            ("paper", "rock") => "win",
            ("scissors", "paper") => "win",
            _ => "lose"
        };
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        WaitingPlayers.RemoveAll(p => p.ConnectionId == Context.ConnectionId);
        if (ActiveGames.TryGetValue(Context.ConnectionId, out var game))
        {
            var otherPlayerId = game.Player1.ConnectionId == Context.ConnectionId 
                ? game.Player2.ConnectionId 
                : game.Player1.ConnectionId;
                
            ActiveGames.Remove(Context.ConnectionId);
            ActiveGames.Remove(otherPlayerId);
        }
        return base.OnDisconnectedAsync(exception);
    }
}

public class Player
{
    public string ConnectionId { get; }
    public string Name { get; }
    public string? Choice { get; set; }

    public Player(string connectionId, string name)
    {
        ConnectionId = connectionId;
        Name = name;
    }
}

public class Game
{
    public Player Player1 { get; }
    public Player Player2 { get; }

    public Game(Player player1, Player player2)
    {
        Player1 = player1;
        Player2 = player2;
    }

    public void MakeChoice(string connectionId, string choice)
    {
        if (Player1.ConnectionId == connectionId)
            Player1.Choice = choice;
        else if (Player2.ConnectionId == connectionId)
            Player2.Choice = choice;
    }

    public bool IsBothPlayersChosen() => 
        !string.IsNullOrEmpty(Player1.Choice) && !string.IsNullOrEmpty(Player2.Choice);
}