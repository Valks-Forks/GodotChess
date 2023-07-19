using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace GodotChess;

public partial class Piece : Area2D
{
    [Export] private Texture2D _whiteTexture;
    [Export] private Texture2D _blackTexture;

    private Sprite2D _sprite;
    private Game _game;
    private Node2D _hints;
    private PackedScene _hintPrefab;

    protected Board Board;
    private Side _side { get; set; }
    public int MoveAmount { get; set; }
    public SquareLocation Location { get; set; }
    public Type PieceType { get; set; }
    public bool HasMoved { get; set; }

    public enum Type
    {
        Pawn,
        Knight,
        Bishop,
        Queen,
        Rook,
        King
    }

    public record MoveContext(SquareLocation Value, bool IsEnPassant = false, SquareLocation EnPassantLocation = null)
    {
        public SquareLocation Value { get; } = Value;
        public bool IsEnPassant { get; } = IsEnPassant;
        public SquareLocation EnPassantLocation { get; } = EnPassantLocation;
    }

    public override void _Ready()
    {
        _sprite = GetNode<Sprite2D>("Sprite2D");
        Board = GetNode<Board>("/root/Game/Board");
        _game = GetNode<Game>("/root/Game");
        _hints = GetNode<Node2D>("/root/Game/Board/Hints");
        _hintPrefab = ResourceLoader.Load<PackedScene>("res://prefabs/move_hint.tscn");
        
        InputEvent += OnClick;
    }

    public virtual HashSet<MoveContext> GenerateMoves()
    {
        return new HashSet<MoveContext>();
    }

    protected void AddIfCapturable(SquareLocation location, ref HashSet<MoveContext> moves)
    {
        if (Board.GetSquare(location).IsOccupied) // TODO: deny capture if king will be checked
        {
            Add(location, ref moves);
        }
    }

    protected void AddIfNotCapturable(SquareLocation location, ref HashSet<MoveContext> moves)
    {
        if (!Board.GetSquare(location).IsOccupied) // TODO: deny capture if king will be checked
        {
            Add(location, ref moves);
        }
    }

    protected void AddDirectionUntilObstructed(SquareLocation delta, ref HashSet<MoveContext> moves)
    {
        var distance = 0;

        while (true)
        {
            distance++;
            var location = GetDeltaLocation(delta * distance);

            if (Add(location, ref moves) || Board.GetSquare(location).IsOccupied) return;
        }
    }
    
    protected bool Add(SquareLocation location, ref HashSet<MoveContext> moves)
    {
        if (SquareLocation.IsInvalid(location)) return true;
        
        var square = Board.GetSquare(location);

        if (square.IsOccupied && square.OccupyingPiece._side == _side) return true;
        
        moves.Add(new MoveContext(location));
        return false;
    }

    protected SquareLocation GetDeltaLocation(SquareLocation delta)
    {
        return Location + delta * Game.SideMultiplier;
    }
    
    protected void WithDeltaLocation(SquareLocation delta, Action<SquareLocation> action)
    {
        var result = GetDeltaLocation(delta);

        if (SquareLocation.IsValid(result))
        {
            action.Invoke(result);
        }
    }
    
    public void ColorAs(Side side)
    {
        _sprite.Texture = side == Side.White ? _whiteTexture : _blackTexture;
        _side = side;
    }

    public void DeleteHints()
    {
        foreach (var hint in _hints.GetChildren())
        {
            hint.QueueFree();
        }
    }

    private void OnClick(Node _, InputEvent input, long _1)
    {
        if (!input.IsPressed() || Game.SideMoving != _side) return;

        DeleteHints();
        var locations = RemoveDuplicateMoves(GenerateMoves());

        foreach (var location in locations)
        {
            var hint = _hintPrefab.Instantiate<MoveHint>();
            var move = ConvertLocationToMove(location);
            hint.HintedMove = move;
            hint.HintedPiece = this;
            hint.Position = move.TargetLocation.AsRelativePosition();
            _hints.AddChild(hint);
        }
    }

    private Move ConvertLocationToMove(MoveContext context)
    {
        var isCapture = Board.GetSquare(context.Value).IsOccupied;
        // TODO: account for checks, mates and promotions

        return new Move { Type = PieceType, SourceLocation = Location, TargetLocation = context.Value,
            IsCapture = isCapture, IsEnPassant = context.IsEnPassant, EnPassantLocation = context.EnPassantLocation };
    }

    private static HashSet<MoveContext> RemoveDuplicateMoves(IReadOnlyCollection<MoveContext> source)
    {
        var locations = source.Select(move => move.Value).ToHashSet();
        var output = new HashSet<MoveContext>();

        foreach (var location in locations)
        {
            output.Add(source.First(move => move.Value == location));
        }

        return output;
    } 

    public static string EncodeTypeToNotation(Type type)
    {
        return type switch
        {
            Type.Pawn => null,
            Type.Knight => "N",
            Type.Bishop => "B",
            Type.Queen => "Q",
            Type.Rook => "R",
            Type.King => "K",
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public static Type DecodeTypeFromNotation(string notation)
    {
        return notation switch
        {
            null => Type.Pawn,
            "P" => Type.Pawn, // alternative for pawns used in Board._pieceSetupMask
            "N" => Type.Knight,
            "B" => Type.Bishop,
            "Q" => Type.Queen,
            "R" => Type.Rook,
            "K" => Type.King,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
