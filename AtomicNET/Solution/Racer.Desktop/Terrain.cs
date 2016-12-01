using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AtomicEngine;
using OpenSimplex;

public class Terrain
{
    private readonly Scene _scene;
    private readonly Material _surfMat;
    private readonly Material _chunkmat;
    private readonly List<Sprite2D> _decors = new List<Sprite2D>();
    private readonly List<CollisionChain2D> _chunks = new List<CollisionChain2D>();

    // Generator configuration
    private const int Chunksize = 20;
    private const int Chunkheight = -100;
    private const float NoiseScaleX = .05f;
    private const float NoiseScaleY = 6;
    private const int SurfaceRepeatPerChunk = 6;
    private const float SurfaceSegmentSize = 0.5f;

    // Generation working variables
    private OpenSimplexNoise _noise = new OpenSimplexNoise();
    private Random _rng = new Random();
    private Vector3 _lastSurfaceExtrusion = Vector3.Right*Chunksize;
    private int _discard = 0;
    
    void GenerateChunk(int startx)
    {
        // We create a node and position where the chunk starts
        Node node = _scene.CreateChild();
        node.SetPosition2D(startx, 0);

        // We create components to render the geometries of the surface and the ground
        var groundComponent = node.CreateComponent<CustomGeometry>();
        groundComponent.SetMaterial(_chunkmat);
        groundComponent.BeginGeometry(0, PrimitiveType.TRIANGLE_LIST);

        var surfaceComponent = node.CreateComponent<CustomGeometry>();
        surfaceComponent.SetMaterial(_surfMat);
        surfaceComponent.BeginGeometry(0, PrimitiveType.TRIANGLE_LIST);
        
        // We initialize and add a single entry to the surface points list
        List<Vector2> surface = new List<Vector2>() {new Vector2(0,
            (float)_noise.Evaluate(startx*NoiseScaleX, 0)*NoiseScaleY
            )
        };

        // We translate the last surface extrusion point so it's local relative to the chunk we're creating
        _lastSurfaceExtrusion += Vector3.Left*Chunksize;

        // We //TODO continue
        float incr = SurfaceSegmentSize;
        for (float i = 0; i < Chunksize-float.Epsilon*2; i+=incr)
        {
            float iend = i+incr;
            float tlY = SampleSurface(startx + i);
            float trY = SampleSurface(startx + iend);
            float blY = tlY + Chunkheight;
            float brY = trY + Chunkheight;

            Vector3 bl = new Vector3(i, blY, -10);
            Vector3 tl = new Vector3(i, tlY, -10);
            Vector3 br = new Vector3(iend, brY, -10);
            Vector3 tr = new Vector3(iend, trY, -10);

            //phys
            surface.Add(new Vector2(tr));

            //decor
            CreateDecor(tr+Vector3.Right*startx, tl-tr);

            //surface visual
            Vector2 startV = Vector2.UnitX*(i/Chunksize)*SurfaceRepeatPerChunk;
            Vector2 endV = Vector2.UnitX*(iend/Chunksize*SurfaceRepeatPerChunk);
            //bl
            surfaceComponent.DefineVertex(_lastSurfaceExtrusion);
            surfaceComponent.DefineTexCoord(startV);
            //tl
            surfaceComponent.DefineVertex(tl);
            surfaceComponent.DefineTexCoord(startV-Vector2.UnitY);
            //tr
            surfaceComponent.DefineVertex(tr);
            surfaceComponent.DefineTexCoord(-Vector2.UnitY+endV);
            //bl
            surfaceComponent.DefineVertex(_lastSurfaceExtrusion);
            surfaceComponent.DefineTexCoord(startV);
            //tr
            surfaceComponent.DefineVertex(tr);
            surfaceComponent.DefineTexCoord(-Vector2.UnitY+endV);
            //br
            _lastSurfaceExtrusion = tr + Quaternion.FromAxisAngle(Vector3.Back, 90)*Vector3.NormalizeFast(tr - tl);
            surfaceComponent.DefineVertex(_lastSurfaceExtrusion);
            surfaceComponent.DefineTexCoord(endV);

            //ground
            //bl
            groundComponent.DefineVertex(bl);
            groundComponent.DefineTexCoord(new Vector2(bl/Chunksize));
            //tl
            groundComponent.DefineVertex(tl);
            groundComponent.DefineTexCoord(new Vector2(tl/Chunksize));
            //tr
            groundComponent.DefineVertex(tr);
            groundComponent.DefineTexCoord(new Vector2(tr/Chunksize));
            //bl
            groundComponent.DefineVertex(bl);
            groundComponent.DefineTexCoord(new Vector2(bl/Chunksize));
            //tr
            groundComponent.DefineVertex(tr);
            groundComponent.DefineTexCoord(new Vector2(tr/Chunksize));
            //br
            groundComponent.DefineVertex(br);
            groundComponent.DefineTexCoord(new Vector2(br/Chunksize));
        }
        surfaceComponent.Commit();
        groundComponent.Commit();
        

        CollisionChain2D col = node.CreateComponent<CollisionChain2D>();
        col.SetLoop(false);
        col.SetFriction(10);
        col.SetVertexCount((uint) surface.Count+1);
        _chunks.Add(col);
        
        //Vector3 smoother = Quaternion.FromRotationTo(Vector3.Left, new Vector3(-1,-.3f,0)) * new Vector3(surface[0] - surface[1]);
        //col.SetVertex(0,surface[0] + new Vector2(smoother));

        Vector2 smoother = new Vector2(-incr*.5f, (float) _noise.Evaluate((startx-incr*.5f)*NoiseScaleX, 0)*NoiseScaleY-0.005f);
        col.SetVertex(0,smoother);

        uint c2 = 0;
        foreach (Vector2 surfpoint in surface)
        {
            col.SetVertex(++c2, new Vector2(surfpoint.X, surfpoint.Y));
        }
        
        node.CreateComponent<RigidBody2D>().SetBodyType(BodyType2D.BT_STATIC);
    }

    public Terrain(Scene scene)
    {

        _scene = scene;

        _chunkmat = Cache.Get<Material>("_DBG/Unlit.xml");
        _chunkmat.SetTexture(0,Cache.Get<Texture2D>("scenarios/grasslands/ground.png"));

        _surfMat = Cache.Get<Material>("_DBG/UnlitAlpha.xml");
        _surfMat.SetTexture(0, Cache.Get<Texture2D>("scenarios/grasslands/surface.png"));

        // _decors quick hack
        _decors.Add(Cache.Get<Sprite2D>("scenarios/grasslands/Object/Bush (1).png"));
        _decors.Add(Cache.Get<Sprite2D>("scenarios/grasslands/Object/Bush (2).png"));
        _decors.Add(Cache.Get<Sprite2D>("scenarios/grasslands/Object/Bush (3).png"));
        _decors.Add(Cache.Get<Sprite2D>("scenarios/grasslands/Object/Bush (4).png"));
        _decors.Add(Cache.Get<Sprite2D>("scenarios/grasslands/Object/Mushroom_1.png"));
        _decors.Add(Cache.Get<Sprite2D>("scenarios/grasslands/Object/Mushroom_2.png"));
        _decors.Add(Cache.Get<Sprite2D>("scenarios/grasslands/Object/Stone.png"));
        _decors.Add(Cache.Get<Sprite2D>("scenarios/grasslands/Object/Sign_2.png"));
        _decors.Add(Cache.Get<Sprite2D>("scenarios/grasslands/Object/Tree_1.png"));
        _decors.Add(Cache.Get<Sprite2D>("scenarios/grasslands/Object/Tree_2.png"));
        _decors.Add(Cache.Get<Sprite2D>("scenarios/grasslands/Object/Tree_3.png"));

        foreach (Sprite2D d in _decors)
        {
            d.SetHotSpot(new Vector2(0.5f, 0.1f));
        }

        Sprite2D crateSprite = Cache.Get<Sprite2D>("scenarios/grasslands/Object/Crate.png");

        Random rng = new Random();
        for (int i = 0; i < Chunksize*400; i+=Chunksize)
        {
            GenerateChunk(i);

            //boxes
            if (rng.Next(1) == -10)
            {
                var c = Racer2D.CreateSpriteNode(crateSprite, 2);
                c.SetPosition(new Vector3(i + rng.Next(8), 20, -5));
                var box = c.CreateComponent<CollisionBox2D>();
                box.SetSize(.8f, .8f);
                box.SetDensity(0.1f);
                box.SetRestitution(0.2f);
                box.SetFriction(0.05f);
            }

        }

    }


    public float SampleSurface(float posX)
    {
        return (float)_noise.Evaluate(posX*NoiseScaleX, 0)*NoiseScaleY;
    }

    void CreateDecor(Vector3 position, Vector3 leftVector)
    {
        _discard++;
        if (_discard % 3 != 0)
            return;

        double chance = _noise.Evaluate(position.X * 0.2f, 0)+1;
        Node n = null;
        bool isTree = false;

        if (chance < 1.2f)
        {
            //grasses
            int r = _rng.Next(6);

            if (r < 4)
            {
                n = Racer2D.CreateSpriteNode(_decors[r], 3, false);
            }

        }
        else if (chance < 1.3f)
        {
            int r = _rng.Next(4)+4;
            n = Racer2D.CreateSpriteNode(_decors[r], 4, false);
        }
        else if (chance < 1.7f)
        {
            if (Math.Abs(leftVector.Y) < 0.1f) //Vector3.Dot(leftVector, Vector3.Left))
            {
                int r = _rng.Next(3) + 8;
                n = Racer2D.CreateSpriteNode(_decors[r], 4, false);
                n.Scale2D = n.Scale2D *= 1 + _rng.Next(30)/100f;
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

}

