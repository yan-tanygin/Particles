using System.Numerics;

namespace CollidingParticles.Physics;

public sealed class Particle
{
    public Particle(Vector2 position, Vector2 velocity, float radius, float mass)
    {
        Position = position;
        Velocity = velocity;
        Radius = radius;
        Mass = mass;
    }

    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public float Radius { get; }
    public float Mass { get; }
}
