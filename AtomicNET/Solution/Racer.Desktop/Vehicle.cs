using System;
using System.Collections.Generic;
using AtomicEngine;

public class Vehicle : CSComponent
{
    private List<Wheel> _wheels = new List<Wheel>();
    private Input _input = AtomicNET.GetSubsystem<Input>();
    private RigidBody2D _rigidBody;
    private ParticleEmitter2D _exhaustParticles;
    private int _horsePower;
    private int _maxSpdFwd;
    private int _maxSpdBwd;
    private int _rollForce;

    private struct Wheel
    {
        private readonly RigidBody2D _rigidBody2D;
        private readonly ParticleEmitter2D _particleEmitter;
        private readonly float _particlesDistance;

        public Wheel(RigidBody2D rigidBody, ParticleEmitter2D particleEmitter, float particlesDistance)
        {
            _rigidBody2D = rigidBody; _particleEmitter = particleEmitter; _particlesDistance = particlesDistance;
        }

        public void ApplyNonLinearTorque(int power, int targetSpeed)
        {
            float fraction = _rigidBody2D.AngularVelocity/targetSpeed;
            float mult = fraction > 0 ? 1-fraction : 1;
            _rigidBody2D.ApplyTorque(mult*power, true);
        }

        public void EmitSurfaceParticles()
        {
            Vector3 nearestSurfPoint = Racer2D.GetSurfacePointClosestToPoint(_rigidBody2D.Node);
            float contactDistance = Vector3.Distance(_rigidBody2D.Node.Position, nearestSurfPoint);
            if (contactDistance > _particlesDistance)
            {
                _particleEmitter.Effect.StartColor = new Color(0, 0, 0, 0);
            }
            else
            {
                _particleEmitter.Effect.StartColor = Color.White;
                _particleEmitter.Node.Position = nearestSurfPoint;
            }
        }
    }
    
    public Vehicle CreateChassis(
        Vector2 colliderCenter, float colliderRadius, int massDensity, Vector3 exhaustPosition, ParticleEffect2D exhaustParticles,
        int horsePower, int maxSpeedFwd, int maxSpeedBwd, int rollForce)
    {
        // We set out private fields
        _horsePower = horsePower;
        _maxSpdFwd = maxSpeedFwd;
        _maxSpdBwd = maxSpeedBwd;
        _rollForce = rollForce;
        _rigidBody = GetComponent<RigidBody2D>();

        // We add the collider (circle collider at the moment)
        var col = Racer2D.AddCollider<CollisionCircle2D>(Node, dens:massDensity, fric:0);
        col.SetRadius(colliderRadius);
        col.SetCenter(colliderCenter);
        
        // We create the exhaust particle system
        var exhaustParticlesNode = Node.CreateChild();
        exhaustParticlesNode.SetPosition(exhaustPosition);
        _exhaustParticles = exhaustParticlesNode.CreateComponent<ParticleEmitter2D>();
        _exhaustParticles.SetEffect(exhaustParticles);
        
        // We return the Vehicle for convenience, since this function is intended to be the vehicle's init function
        return this;
    }

    public Node CreateWheel(
        Sprite2D sprite, Vector2 relPos, float radius, int suspensionFrequency, float suspensionDamping, ParticleEffect2D particles,
        float distanceToEmitParticles)
    {
        Node wheelNode = Racer2D.CreateSpriteNode(sprite);
        wheelNode.SetPosition2D(relPos);

        // CreateSpriteNode adds a RigidBody for us, so we get it here
        RigidBody2D wheelRigidBody = wheelNode.GetComponent<RigidBody2D>();
        // We activate CCD
        wheelRigidBody.SetBullet(true);

        Racer2D.AddCollider<CollisionCircle2D>(wheelNode).SetRadius(radius);
        
        // The Box2D wheel joint provides spring for simulating suspension
        ConstraintWheel2D wheelJoint = Node.CreateComponent<ConstraintWheel2D>();
        wheelJoint.SetOtherBody(wheelRigidBody);
        wheelJoint.SetAnchor(relPos);
        wheelJoint.SetAxis(Vector2.UnitY);
        wheelJoint.SetFrequencyHz(suspensionFrequency);
        wheelJoint.SetDampingRatio(suspensionDamping);
        
        // Each wheel has a particle emitter to emit particles when it's in contact with the surface
        Node particlesNode = Node.Scene.CreateChild();
        particlesNode.SetPosition(new Vector3(relPos.X, relPos.Y, 14));
        ParticleEmitter2D pe = particlesNode.CreateComponent<ParticleEmitter2D>();
        pe.SetEffect(particles);

        // We create a new Wheel struct and add to the _wheels list
        _wheels.Add(new Wheel(wheelRigidBody, pe, distanceToEmitParticles));

        return wheelNode;
    }

    public Node CreateHead(Sprite2D sprite, Vector3 relativePosition, float colliderRadius, Vector2 neckAnchor)
    {
        Node head = Racer2D.CreateSpriteNode(sprite);
        head.SetPosition(relativePosition);
        Racer2D.AddCollider<CollisionCircle2D>(head).SetRadius(colliderRadius);

        // This is the actual neck joint
        ConstraintRevolute2D joint = head.CreateComponent<ConstraintRevolute2D>();
        joint.SetOtherBody(_rigidBody);
        joint.SetAnchor(neckAnchor);
        
        // This is the spring, it's attached to the body with an offset
        ConstraintDistance2D spring = head.CreateComponent<ConstraintDistance2D>();
        spring.SetOtherBody(_rigidBody);
        spring.SetOwnerBodyAnchor(-Vector2.UnitY*2);
        spring.SetOtherBodyAnchor(Node.WorldToLocal2D(head.WorldPosition2D-Vector2.UnitY*2));
        spring.SetFrequencyHz(3);
        spring.SetDampingRatio(0.4f);

        return head;
    }

    // Update is called once per frame
    private void Update(float dt)
    {
        // Wheel controls
        bool isBraking = _input.GetKeyDown((int) SDL.SDL_Keycode.SDLK_DOWN);
        bool isAccelerating = _input.GetKeyDown((int) SDL.SDL_Keycode.SDLK_UP);
        
        foreach (Wheel wheel in _wheels)
        {
            // We give priority to braking
            if (isBraking)
            {
                wheel.ApplyNonLinearTorque(_horsePower, _maxSpdBwd);
            }
            else if (isAccelerating)
            {
                wheel.ApplyNonLinearTorque(-_horsePower, -_maxSpdFwd);
            }
            // We emit surface particles
            wheel.EmitSurfaceParticles();
        }

        // Roll controls
        if (_input.GetKeyDown((int) SDL.SDL_Keycode.SDLK_LEFT))
            _rigidBody.ApplyTorque(_rollForce,true);
        if (_input.GetKeyDown((int) SDL.SDL_Keycode.SDLK_RIGHT))
            _rigidBody.ApplyTorque(_rollForce*-1,true);
        // Debug control for when you rolled over
        if (_input.GetKeyDown((int) SDL.SDL_Keycode.SDLK_SPACE))
            _rigidBody.SetAngularVelocity(2);

        // We apply bit of the vehicle's velocity to the exhaust particles so they look OK
        Vector2 currentVelocity = _rigidBody.GetLinearVelocity();
        _exhaustParticles.Effect.Speed = Math.Abs(100 + currentVelocity.LengthFast * 20);
        _exhaustParticles.Effect.SpeedVariance = currentVelocity.LengthFast * 10;
        _exhaustParticles.Effect.Angle = -Node.Rotation2D + 
            (float)(Math.Atan2(currentVelocity.X - 5, -currentVelocity.Y) * (360 / (Math.PI * 2))) - 90;

    }
}