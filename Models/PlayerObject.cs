using System.Formats.Asn1;
using Silk.NET.Maths;
using TheAdventure;

namespace TheAdventure.Models;

public class PlayerObject : RenderableGameObject
{
    public enum PlayerStateDirection
    {
        None = 0,
        Down,
        Up,
        Left,
        Right,
    }

    public enum PlayerState
    {
        None = 0,
        Idle,
        Move,
        Attack,
        GameOver,
        Jump
    }

    private int _pixelsPerSecond = 192;
    private float sprintMultiplier = 2.0f;
    private bool _isJumping = false;
    private float _jumpVelocity = 0;
    private const float Gravity = 20.0f; // Ajustat pentru a aduce personajul mai repede la sol
    private const float JumpForce = -5.0f; // Ajustat pentru a reduce înălțimea săriturii
    private float _groundLevel;

    public (PlayerState State, PlayerStateDirection Direction) State { get; private set; }

    public PlayerObject(SpriteSheet spriteSheet, int x, int y) : base(spriteSheet, (x, y))
    {
        _groundLevel = y;
        SetState(PlayerState.Idle, PlayerStateDirection.Down);
    }

    public void SetState(PlayerState state, PlayerStateDirection direction)
    {
        if (State.State == PlayerState.GameOver) return;
        if (State.State == state && State.Direction == direction)
        {
            return;
        }
        else if (state == PlayerState.None && direction == PlayerStateDirection.None)
        {
            SpriteSheet.ActivateAnimation(null);
        }
        else if (state == PlayerState.GameOver)
        {
            SpriteSheet.ActivateAnimation(Enum.GetName(state));
        }
        else
        {
            var animationName = Enum.GetName<PlayerState>(state) + Enum.GetName<PlayerStateDirection>(direction);
            SpriteSheet.ActivateAnimation(animationName);
        }
        State = (state, direction);
    }

    public void GameOver()
    {
        SetState(PlayerState.GameOver, PlayerStateDirection.None);
    }

    public void Attack(bool up, bool down, bool left, bool right)
    {
        if (State.State == PlayerState.GameOver) return;
        var direction = State.Direction;
        if (up)
        {
            direction = PlayerStateDirection.Up;
        }
        else if (down)
        {
            direction = PlayerStateDirection.Down;
        }
        else if (left)
        {
            direction = PlayerStateDirection.Left;
        }
        else if (right)
        {
            direction = PlayerStateDirection.Right;
        }
        SetState(PlayerState.Attack, direction);
    }

    public void Move(bool up, bool down, bool left, bool right)
    {
        if (State.State == PlayerState.GameOver) return;

        float speed = _pixelsPerSecond / 60.0f; // assuming 60 FPS
        if (Input.IsSPressed())
        {
            speed *= sprintMultiplier;
        }

        var x = Position.X;
        var y = Position.Y;

        if (up)
        {
            y -= (int)speed;
        }
        if (down)
        {
            y += (int)speed;
        }
        if (left)
        {
            x -= (int)speed;
        }
        if (right)
        {
            x += (int)speed;
        }

        if (Input.IsSpacePressed() && !_isJumping)
        {
            _isJumping = true;
            _jumpVelocity = JumpForce;
        }

        if (_isJumping)
        {
            _jumpVelocity += Gravity * 0.016f; // assuming 60 FPS
            y += (int)_jumpVelocity;

            if (y >= _groundLevel)
            {
                y = (int)_groundLevel;
                _isJumping = false;
                _jumpVelocity = 0;
            }
        }

        if (y < Position.Y && !_isJumping)
        {
            SetState(PlayerState.Move, PlayerStateDirection.Up);
        }
        else if (y > Position.Y)
        {
            SetState(PlayerState.Move, PlayerStateDirection.Down);
        }
        else if (x > Position.X)
        {
            SetState(PlayerState.Move, PlayerStateDirection.Right);
        }
        else if (x < Position.X)
        {
            SetState(PlayerState.Move, PlayerStateDirection.Left);
        }
        else if (x == Position.X && y == Position.Y)
        {
            SetState(PlayerState.Idle, State.Direction);
        }

        Position = (x, y);
    }
}
