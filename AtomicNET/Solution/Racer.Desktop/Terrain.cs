using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AtomicEngine;
using OpenSimplex;

public class Terrain
{
    OpenSimplexNoise noise = new OpenSimplexNoise();

    private Material surfMat;

    List<Sprite2D> decors = new List<Sprite2D>();

    private Material chunkmat;
    List<CollisionChain2D> chunks = new List<CollisionChain2D>();
    const int chunksize = 20;
    const int chunkheight = -100;
    private float noiseScaleX = .05f;
    private float noiseScaleY = 6;

    private Vector3 lastSurfaceExtrusion = Vector3.Right*chunksize;

    private int surfaceVisualRepeatPerChunk = 6;

    private readonly Scene _scene;

    public Terrain(Scene scene)
    {

        _scene = scene;

        chunkmat = Cache.Get<Material>("_DBG/Unlit.xml");
        chunkmat.SetTexture(0,Cache.Get<Texture2D>("scenarios/grasslands/ground.png"));

        surfMat = Cache.Get<Material>("_DBG/UnlitAlpha.xml");
        surfMat.SetTexture(0, Cache.Get<Texture2D>("scenarios/grasslands/surface.png"));
        //surfMat.SetTexture(0, cache.GetResource<Texture2D>("_DBG/dbg.png"));

        // decors quick crappy hack
        decors.Add(Cache.Get<Sprite2D>("scenarios/grasslands/Object/Bush (1).png"));
        decors.Add(Cache.Get<Sprite2D>("scenarios/grasslands/Object/Bush (2).png"));
        decors.Add(Cache.Get<Sprite2D>("scenarios/grasslands/Object/Bush (3).png"));
        decors.Add(Cache.Get<Sprite2D>("scenarios/grasslands/Object/Bush (4).png"));
        decors.Add(Cache.Get<Sprite2D>("scenarios/grasslands/Object/Mushroom_1.png"));
        decors.Add(Cache.Get<Sprite2D>("scenarios/grasslands/Object/Mushroom_2.png"));
        decors.Add(Cache.Get<Sprite2D>("scenarios/grasslands/Object/Stone.png"));
        decors.Add(Cache.Get<Sprite2D>("scenarios/grasslands/Object/Sign_2.png"));
        decors.Add(Cache.Get<Sprite2D>("scenarios/grasslands/Object/Tree_1.png"));
        decors.Add(Cache.Get<Sprite2D>("scenarios/grasslands/Object/Tree_2.png"));
        decors.Add(Cache.Get<Sprite2D>("scenarios/grasslands/Object/Tree_3.png"));

        foreach (Sprite2D d in decors)
        {
            d.SetHotSpot(new Vector2(0.5f, 0.1f));
        }

        Sprite2D crateSprite = Cache.Get<Sprite2D>("scenarios/grasslands/Object/Crate.png");

        Random rng = new Random();
        for (int i = 0; i < chunksize*400; i+=chunksize)
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
        return (float)noise.Evaluate(posX*noiseScaleX, 0)*noiseScaleY;
    }

    void GenerateChunk(int startx)
    {
        Node n = _scene.CreateChild();
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

            Vector3 bl = new Vector3(i, blY, -10);
            Vector3 tl = new Vector3(i, tlY, -10);
            Vector3 br = new Vector3(iend, brY, -10);
            Vector3 tr = new Vector3(iend, trY, -10);

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

    private int discard = 0;
    Random rng = new Random();
    void CreateDecor(Vector3 position, Vector3 leftVector)
    {
        return;
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
                n = Racer2D.CreateSpriteNode(decors[r], 3, false);
            }

        }
        else if (chance < 1.3f)
        {
            int r = rng.Next(4)+4;
            n = Racer2D.CreateSpriteNode(decors[r], 4, false);
        }
        else if (chance < 1.7f)
        {
            if (Math.Abs(leftVector.Y) < 0.1f) //Vector3.Dot(leftVector, Vector3.Left))
            {
                int r = rng.Next(3) + 8;
                n = Racer2D.CreateSpriteNode(decors[r], 4, false);
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

