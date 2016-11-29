using System;
using System.Collections.Generic;
using System.Diagnostics;
using AtomicEngine;
using AtomicPlayer;
using OpenSimplex;


public class AtomicMain : AppDelegate
{
    ResourceCache cache;
    Scene scene;
    Graphics graphics;
    Viewport viewport;
    Camera camera;
    Input input;

    private Node currentVehicle;

    List<Sprite2D> decors = new List<Sprite2D>();
    
    public override void Start()
    {
        cache = GetSubsystem<ResourceCache>();
        input = GetSubsystem<Input>();
        Renderer renderer = GetSubsystem<Renderer>();
        graphics = GetSubsystem<Graphics>();
        viewport = renderer.GetViewport(0);

        scene = new Scene();
        scene.CreateComponent<Octree>().SetSize(new BoundingBox(1,100), 3);
        viewport.Scene = scene;

        camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        camera.Node.Position = new Vector3(15, 0, -1);
        camera.Orthographic = true;
        camera.OrthoSize = 26;
        viewport.Camera = camera;


        Node bg = camera.Node.CreateChild("bg");
        var bgspr = bg.CreateComponent<StaticSprite2D>();
        bgspr.SetSprite(cache.GetResource<Sprite2D>("scenarios/grasslands/bg.png"));
        bg.SetPosition(new Vector3(0,0,100));
        bg.SetScale2D(Vector2.One*5);

        
        chunkmat = cache.GetResource<Material>("_DBG/Unlit.xml");
        chunkmat.SetTexture(0,cache.GetResource<Texture2D>("scenarios/grasslands/ground.png"));

        surfMat = cache.GetResource<Material>("_DBG/UnlitAlpha.xml");
        surfMat.SetTexture(0, cache.GetResource<Texture2D>("scenarios/grasslands/surface.png"));
        //surfMat.SetTexture(0, cache.GetResource<Texture2D>("_DBG/dbg.png"));

        PhysicsWorld2D pw = scene.CreateComponent<PhysicsWorld2D>();
        pw.SetContinuousPhysics(true);
        

#if DEBUG
        scene.CreateComponent<DebugRenderer>();
        SubscribeToEvent<PostRenderUpdateEvent>(e => RenderDebug());
#endif

        SubscribeToEvent<UpdateEvent>(e => Update());



        // decors quick crappy hack
        decors.Add(GetSprite("scenarios/grasslands/Object/Bush (1).png"));
        decors.Add(GetSprite("scenarios/grasslands/Object/Bush (2).png"));
        decors.Add(GetSprite("scenarios/grasslands/Object/Bush (3).png"));
        decors.Add(GetSprite("scenarios/grasslands/Object/Bush (4).png"));

        decors.Add(GetSprite("scenarios/grasslands/Object/Mushroom_1.png"));
        decors.Add(GetSprite("scenarios/grasslands/Object/Mushroom_2.png"));
        decors.Add(GetSprite("scenarios/grasslands/Object/Stone.png"));
        decors.Add(GetSprite("scenarios/grasslands/Object/Sign_2.png"));

        decors.Add(GetSprite("scenarios/grasslands/Object/Tree_1.png"));
        decors.Add(GetSprite("scenarios/grasslands/Object/Tree_2.png"));
        decors.Add(GetSprite("scenarios/grasslands/Object/Tree_3.png"));

        foreach (Sprite2D d in decors)
        {
            d.SetHotSpot(new Vector2(0.5f, 0.1f));
        }


        Sprite2D crateSprite = GetSprite("scenarios/grasslands/Object/Crate.png");

        Sprite2D[] cloudSprites = {
            GetSprite("scenarios/cloud1.png"),
            GetSprite("scenarios/cloud2.png"),
            GetSprite("scenarios/cloud3.png"),
        };
        Random rng = new Random();
        for (int i = 0; i < chunksize*200; i+=chunksize)
        {
            GenerateChunk(i);


            //innefficient clouds :P
            var n = CreateSpriteNode(scene, cloudSprites[rng.Next(3)], 5, false);
            n.SetPosition(new Vector3(i + rng.Next(8), rng.Next(15) + 15, 15));
            clouds.Add(n);

            //boxes
            if (rng.Next(10) == 0)
            {
                var c = CreateSpriteNode(scene, crateSprite, 2, true);
                c.SetPosition(new Vector3(i + rng.Next(8), 20, -5));
                var box = c.CreateComponent<CollisionBox2D>();
                box.SetSize(.8f, .8f);
                box.SetDensity(0.1f);
                box.SetRestitution(0.2f);
                box.SetFriction(0.05f);
            }

        }
        

        
        //for (float i = 0; i < chunksize*200; i+=0.2f)
        //{
        //    CreateDecor(i);
        //}

        //foreach (CollisionChain2D col in chunks)
        //{
        //    col.SetEnabled(false);
        //}

        currentVehicle = CreateTruck(new Vector2(2,3));

    }

    List<Node> clouds = new List<Node>();

    
    private int discard = 0;
    Random rng = new Random();
    void CreateDecor(Vector3 position, Vector3 leftVector)
    {
        discard++;
        if (discard % 3 != 0)
            return;

        double chance = noise.Evaluate(position.X * 0.2f, 0)+1;
        Node n = null;
        bool isTree = false;

        if (chance < 1.2f)
        {
            //grasses
            int r = rng.Next(6);

            if (r < 4)
            {
                n = CreateSpriteNode(scene, decors[r], 3, false);
            }

        }
        else if (chance < 1.3f)
        {
            int r = rng.Next(4)+4;
            n = CreateSpriteNode(scene, decors[r], 4, false);
        }
        else if (chance < 1.7f)
        {
            if (Math.Abs(leftVector.Y) < 0.1f) //Vector3.Dot(leftVector, Vector3.Left))
            {
                int r = rng.Next(3) + 8;
                n = CreateSpriteNode(scene, decors[r], 4, false);
                isTree = true;
            }
        }

        if (n != null)
        {
            n.SetPosition(position+Vector3.Forward*15);
            if (!isTree)
            {
                Quaternion transformation = Quaternion.FromRotationTo(Vector3.Left, leftVector); //todo use atan2??
                n.SetRotation(transformation);
            }
            else
            {
                //correct offset hack fixme
                n.Translate(-n.Up*0.1f);
            }
        }

    }

    //void PlaceOnGroundAtX(Sprite2D sprite, float posX)
    //{
    //    //Vector2 pos = GetSubsystem<PhysicsWorld2D>().Rayc
    //}

    struct WheelData
    {
        public RigidBody2D rb;
        public ParticleEmitter2D pe;
    }

    private List<WheelData> activeWheels = new List<WheelData>();

    Node CreateTruck(Vector2 position)
    {

        var basenode = CreateSpriteNode(scene, GetSprite("characters/truck/vehicle.png"), 1.4f);
        AddCollider<CollisionCircle2D>(basenode, dens:6).SetRadius(1f);

        var wspr = GetSprite("characters/truck/wheel.png");

        var w1 = CreateWheel(wspr, basenode, new Vector2(1.5f,-1.5f));
        var w2 = CreateWheel(wspr, basenode, new Vector2(-1.8f,-1.5f));
        
        //head
        var head = CreateSpriteNode(scene, GetSprite("characters/truck/head.png"));
        head.SetPosition(new Vector3(-1,2.7f,-1));
        AddCollider<CollisionCircle2D>(head).SetRadius(1f);

        var joint = head.CreateComponent<ConstraintRevolute2D>();
        joint.SetOtherBody(basenode.GetComponent<RigidBody2D>());
        joint.SetAnchor(new Vector2(-1,1.8f));

        var spring = head.CreateComponent<ConstraintDistance2D>();
        spring.SetOtherBody(basenode.GetComponent<RigidBody2D>());
        spring.SetOwnerBodyAnchor(-Vector2.UnitY*2);
        spring.SetOtherBodyAnchor(basenode.WorldToLocal2D(head.WorldPosition2D-Vector2.UnitY*2));
        spring.SetFrequencyHz(3);
        spring.SetDampingRatio(0.4f);


        //exhaust parts
        var ep = basenode.CreateChild();
        ep.SetPosition(new Vector3(-2f,-1,14));
        pe = ep.CreateComponent<ParticleEmitter2D>();
        pe.SetEffect(cache.GetResource<ParticleEffect2D>("particles/smoke.pex"));
        
        foreach (Node node in new []{basenode, w1, w2, head})
        {
            node.Translate2D(position);
        }

        return basenode;
    }

    private ParticleEmitter2D pe;

    private Node CreateWheel(Sprite2D wspr, Node basenode, Vector2 relPos)
    {
        var w1 = CreateSpriteNode(scene, wspr);
        w1.GetComponent<RigidBody2D>().SetBullet(true);
        var c = AddCollider<CollisionCircle2D>(w1);
        c.SetRadius(1.25f);
        
        //ConstraintRevolute2D wj1 = basenode.CreateComponent<ConstraintRevolute2D>();
        //wj1.SetOtherBody(w1.GetComponent<RigidBody2D>());
        //w1.SetPosition2D(relPos);
        //wj1.SetAnchor(relPos);
        
        ConstraintWheel2D wj1 = basenode.CreateComponent<ConstraintWheel2D>();
        wj1.SetOtherBody(w1.GetComponent<RigidBody2D>());
        w1.SetPosition2D(relPos);
        wj1.SetAnchor(relPos);
        wj1.SetAxis(Vector2.UnitY);
        wj1.SetFrequencyHz(4);
        wj1.SetDampingRatio(0.4f);

        var ep = scene.CreateChild();
        ep.SetPosition(new Vector3(-2f,-1,14));
        pe = ep.CreateComponent<ParticleEmitter2D>();
        pe.SetEffect(cache.GetResource<ParticleEffect2D>("particles/dust.pex"));

        activeWheels.Add( new WheelData { rb = w1.GetComponent<RigidBody2D>(), pe = pe} );

        return w1;
    }

    /// <summary>
    /// return surface point closest to wheel's center
    /// </summary>
    Vector3 WheelContactHack(Node wheel)
    {

        // sample various points next to the wheel
        List<Vector2> points = new List<Vector2>();
        for (float xOffset = -1; xOffset < 1; xOffset+=0.02f)
        {
            float y = SampleSurface(wheel.Position.X + xOffset);
            points.Add(new Vector2(wheel.Position.X + xOffset, y));
        }

        // get the closest one
        float lastDist = float.MaxValue;
        Vector2 closestPoint = Vector2.Zero;
        foreach (Vector2 point in points)
        {
            float pointDist = Vector2.Distance(wheel.Position2D, point);
            if (pointDist < lastDist)
            {
                closestPoint = point;
                lastDist = pointDist;
            }
        }

        return new Vector3(closestPoint.X, closestPoint.Y, 2);
    }

    Sprite2D GetSprite(string path)
    {
        return cache.GetResource<Sprite2D>(path);
    }

    T AddCollider<T>(Node node, float fric = 1, float dens = 1, float elas = 0) where T: CollisionShape2D
    {
        CollisionShape2D s = node.CreateComponent<T>();
        s.SetFriction(fric);
        s.SetDensity(dens);
        s.SetRestitution(elas);
        return (T)s;
    }

    Node CreateSpriteNode(Node parent, Sprite2D sprite, float scale = 1f, bool addRB = true)
    {
        Node n = parent.CreateChild();
        n.SetScale2D(Vector2.One*scale);
        n.CreateComponent<StaticSprite2D>().SetSprite(sprite);
        if (addRB)
            n.CreateComponent<RigidBody2D>().SetBodyType(BodyType2D.BT_DYNAMIC);
        return n;
    }

    private int currentChunk = 0;
    void Update()
    {

        if (input.GetKeyDown((int) SDL.SDL_Keycode.SDLK_DOWN))
            foreach (WheelData wheel in activeWheels)
            {
                wheel.rb.ApplyTorque(400,true);
            }
        else if (input.GetKeyDown((int) SDL.SDL_Keycode.SDLK_UP))
            foreach (WheelData wheel in activeWheels)
            {
                wheel.rb.ApplyTorque(-400,true);
            }


        //hack
        foreach (WheelData wheel in activeWheels)
        {
            Vector3 nearestSurfPoint = WheelContactHack(wheel.rb.Node);

            float contactDistance = Vector3.Distance(wheel.rb.Node.Position, nearestSurfPoint);

            if (contactDistance > 2.6f)
            {
                // dunno how to disable emission :(
                wheel.pe.Effect.StartColor = new Color(0,0,0,0);
            }
            else
            {
                //wheel.pe.Enabled = true;
                wheel.pe.Effect.StartColor = Color.White;
                wheel.pe.Node.Position = nearestSurfPoint;
            }
        }

        //hack :P
        foreach (Node cloud in clouds)
        {
            cloud.Translate2D(Vector2.UnitX*0.05f);
        }


        if (input.GetKeyDown((int) SDL.SDL_Keycode.SDLK_LEFT))
            currentVehicle.GetComponent<RigidBody2D>().ApplyTorque(500,true);
        if (input.GetKeyDown((int) SDL.SDL_Keycode.SDLK_RIGHT))
            currentVehicle.GetComponent<RigidBody2D>().ApplyTorque(-500,true);

        // unflip
        if (input.GetKeyDown((int) SDL.SDL_Keycode.SDLK_SPACE))
            currentVehicle.GetComponent<RigidBody2D>().ApplyTorque(-2500,true);

        camera.Node.SetPosition(currentVehicle.Position + Vector3.Back*10);

        Vector2 curvel = currentVehicle.GetComponent<RigidBody2D>().GetLinearVelocity();

        pe.Effect.Speed = Math.Abs(100+curvel.LengthFast*20);
        pe.Effect.SpeedVariance = curvel.LengthFast*10;
        pe.Effect.Angle = -currentVehicle.Rotation2D + (float)(Math.Atan2(curvel.X-5, -curvel.Y)*(360/(Math.PI*2))) - 90;

        //currentChunk = (int)currentVehicle.Position.X/chunksize;
        //chunks[currentChunk].SetEnabled(true);

    }

    void RenderDebug()
    {
        DebugRenderer dbr = scene.GetComponent<DebugRenderer>();
        scene.GetComponent<PhysicsWorld2D>().DrawDebugGeometry(dbr,false);
    }

    OpenSimplexNoise noise = new OpenSimplexNoise(103);

    private Material surfMat;

    private Material chunkmat;
    List<CollisionChain2D> chunks = new List<CollisionChain2D>();
    const int chunksize = 20;
    const int chunkheight = -100;
    private float noiseScaleX = .05f;
    private float noiseScaleY = 11f;

    private Vector3 lastSurfaceExtrusion = Vector3.Right*chunksize;

    private int surfaceVisualRepeatPerChunk = 6;

    private float SampleSurface(float posX)
    {
        return (float)noise.Evaluate(posX*noiseScaleX, 0)*noiseScaleY;
    }

    void GenerateChunk(int startx)
    {
        Node n = scene.CreateChild();
        n.SetPosition2D(startx, 0);

        var g = n.CreateComponent<CustomGeometry>();
        g.SetMaterial(chunkmat);
        g.BeginGeometry(0, PrimitiveType.TRIANGLE_LIST);

        var s = n.CreateComponent<CustomGeometry>();
        s.SetMaterial(surfMat);
        s.BeginGeometry(0, PrimitiveType.TRIANGLE_LIST);
        
        List<Vector2> surface = new List<Vector2>() {new Vector2(0,
            (float)noise.Evaluate((startx)*noiseScaleX, 0)*noiseScaleY
            )
        };

        lastSurfaceExtrusion += Vector3.Left*chunksize;

        float incr = 0.5f;
        for (float i = 0; i < chunksize-float.Epsilon*2; i+=incr)
        {
            float iend = i+incr;
            float tlY = SampleSurface(startx + i);
            float trY = SampleSurface(startx + iend);
            float blY = tlY + chunkheight;
            float brY = trY + chunkheight;

            Vector3 bl = new Vector3(i, blY, 0);
            Vector3 tl = new Vector3(i, tlY, 0);
            Vector3 br = new Vector3(iend, brY, 0);
            Vector3 tr = new Vector3(iend, trY, 0);

            //phys
            surface.Add(new Vector2(tr));

            //decor
            CreateDecor(tr+Vector3.Right*startx, tl-tr);

            //surface visual
            Vector2 startV = Vector2.UnitX*(i/chunksize)*surfaceVisualRepeatPerChunk;
            Vector2 endV = Vector2.UnitX*(iend/chunksize*surfaceVisualRepeatPerChunk);
            //bl
            s.DefineVertex(lastSurfaceExtrusion);
            s.DefineTexCoord(startV);
            //tl
            s.DefineVertex(tl);
            s.DefineTexCoord(startV-Vector2.UnitY);
            //tr
            s.DefineVertex(tr);
            s.DefineTexCoord(-Vector2.UnitY+endV);
            //bl
            s.DefineVertex(lastSurfaceExtrusion);
            s.DefineTexCoord(startV);
            //tr
            s.DefineVertex(tr);
            s.DefineTexCoord(-Vector2.UnitY+endV);
            //br
            lastSurfaceExtrusion = tr + Quaternion.FromAxisAngle(Vector3.Back, 90)*Vector3.NormalizeFast(tr - tl);
            s.DefineVertex(lastSurfaceExtrusion);
            s.DefineTexCoord(endV);

            //ground
            //bl
            g.DefineVertex(bl);
            g.DefineTexCoord(new Vector2(bl/chunksize));
            //tl
            g.DefineVertex(tl);
            g.DefineTexCoord(new Vector2(tl/chunksize));
            //tr
            g.DefineVertex(tr);
            g.DefineTexCoord(new Vector2(tr/chunksize));
            //bl
            g.DefineVertex(bl);
            g.DefineTexCoord(new Vector2(bl/chunksize));
            //tr
            g.DefineVertex(tr);
            g.DefineTexCoord(new Vector2(tr/chunksize));
            //br
            g.DefineVertex(br);
            g.DefineTexCoord(new Vector2(br/chunksize));
        }
        s.Commit();
        g.Commit();
        

        CollisionChain2D col = n.CreateComponent<CollisionChain2D>();
        col.SetLoop(false);
        col.SetFriction(10);
        col.SetVertexCount((uint) surface.Count+1);
        chunks.Add(col);
        
        //Vector3 smoother = Quaternion.FromRotationTo(Vector3.Left, new Vector3(-1,-.3f,0)) * new Vector3(surface[0] - surface[1]);
        //col.SetVertex(0,surface[0] + new Vector2(smoother));

        Vector2 smoother = new Vector2(-incr*.5f, (float) noise.Evaluate((startx-incr*.5f)*noiseScaleX, 0)*noiseScaleY-0.005f);
        col.SetVertex(0,smoother);

        uint c2 = 0;
        foreach (Vector2 surfpoint in surface)
        {
            col.SetVertex(++c2, new Vector2(surfpoint.X, surfpoint.Y));
        }
        
        n.CreateComponent<RigidBody2D>().SetBodyType(BodyType2D.BT_STATIC);

        
    }

    
}
