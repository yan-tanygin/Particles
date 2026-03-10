using System.Numerics;

namespace CollidingParticles.Physics;

public sealed class Particle
{
    public Particle(Vector3 position, Vector3 velocity, float radius, float mass)
    {
        Position = position;
        Velocity = velocity;
        Radius = radius;
        Mass = mass;
    }

    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public float Radius { get; }
    public float Mass { get; }
}
