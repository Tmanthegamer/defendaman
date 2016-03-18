﻿using UnityEngine;
using System.Collections;
using SimpleJSON;

public class AI : MonoBehaviour {
    bool route = false;
    float facing;
    float accuracy = 3.5f;
    public float angleFacing;
    Pair<int, Vector2> lastLocation = new Pair<int, Vector2>(-5, new Vector2());
    int range= 35;
    Vector2 curMove;
    public float xCoord, yCoord;
    private Rigidbody2D rb2d;
    private int speed = 35;
    public double reload = 2.0f;
    Rigidbody2D bullet;
    public int team = -2;
    public int aiID = 0;
    // Use this for initialization
    void Start()
    {
        bullet = (Rigidbody2D)Resources.Load("Prefabs/Bullet", typeof(Rigidbody2D));
        NetworkingManager.Subscribe(UpdateAI, DataType.AI, aiID);
        NetworkingManager.Subscribe(CreateProjectile, DataType.AIProjectile, aiID);
        rb2d = GetComponent<Rigidbody2D>();
    }

    void instantTurret(float reload, int speed, int teamToIgnore, int range)
    {
        this.reload = reload;
        this.speed = speed;
        this.team = teamToIgnore;
        this.range = range;
    }
    void CreateProjectile(JSONClass packet)
    {
        //I created a projectile
        Vector2 attack;
        attack.x = packet["vecX"].AsFloat;
        attack.y = packet["vecY"].AsFloat;
       
        
       

        Rigidbody2D attack2 = (Rigidbody2D)Instantiate(bullet, transform.position, transform.rotation);

        attack2.AddForce(attack * speed);
        Debug.Log(attack);
        attack2.GetComponent<BasicRanged>().teamID = team;
        attack2.GetComponent<BasicRanged>().damage = 10;
        attack2.GetComponent<BasicRanged>().maxDistance = 10;
        reload = 1;
    }

    void UpdateAI(JSONClass packet)
    {
        Debug.Log("Received packet: " + packet.ToString());
        xCoord = packet["x"].AsFloat;
        yCoord = packet["y"].AsFloat;
        angleFacing = packet["facing"].AsFloat;
        Vector2 newPos = new Vector2(xCoord, yCoord);

        transform.rotation = Quaternion.AngleAxis((float)angleFacing, Vector3.forward);
    }

    // Update is called once per frame
    void Update() {
        Vector3 vec = new Vector3();
        Vector3 face = new Vector3();
        float closest = 999;
        int id = 0, realId;
        float facing;
        if (!route)
        {
            curMove = getRoute();
            route = true;
        }
        foreach (var playerData in GameData.LobbyData)
        {
            float dist;

            vec = GameData.PlayerPosition[playerData.Key];
            realId = GameData.MyPlayer.PlayerID;
            dist = Mathf.Sqrt(Mathf.Pow((rb2d.position.x - vec.x), 2) + Mathf.Pow((rb2d.position.y - vec.y), 2));
            //Debug.Log("Player Position: " + vec.x + " " + vec.y + "Distance: " + dist + "Current Closest: " + closest );


            if (dist < closest)
            {
                id = realId;
                closest = dist;
                face = vec;
            }
        }
        if (closest > range)
        {
            id = -1;
            lastLocation.first = -1;
            return;
        }

        float x, y;

        x = face.x - rb2d.position.x;
        y = face.y - rb2d.position.y;
        if (x == 0)
        {
            if (y > 0)
            {
                facing = Mathf.PI / 2;
            }
            else
            {
                facing = 3 * Mathf.PI / 2;
            }
        }
        else
        {
            if (x > 0)
            {
                facing = (float)(Mathf.PI * 2 + System.Math.Atan(y / x)) % (Mathf.PI * 2);

            }
            else
            {
                facing = (float)(Mathf.PI + System.Math.Atan(y / x));// % 360;
            }
        }
        facing = getDegree(facing);
        //facing = Mathf.Tan(xVal / yVal);
        //  Debug.Log("Your position:" + rb2d.position.x + " " + rb2d.position.y);
        //Debug.Log("Closest player position:" + face.x + " " + face.y);
        //Debug.Log(facing);
        if (reload < 0)
        {
            Vector2 attackSpot = new Vector2();
            Vector2 attack = new Vector2();

            if (face.x != lastLocation.second.x || face.y != lastLocation.second.y )
            {
                if (id == lastLocation.first && face != (Vector3)lastLocation.second)
                {

                    attackSpot = getIntersection(face, lastLocation.second, 25);
                    attack = attackSpot;
                }
                else
                {
                    Debug.Log("Doing same spot shot");
                    attack.x = x;
                    attack.y = y;
                }
            }
            else
            {
                Debug.Log("Doing same spot shot");
                attack.x = x;
                attack.y = y;
            }


            //attack.Normalize();
            /*
            System.Random rnd = new System.Random();
            float offset;
            
            offset = (float)(rnd.NextDouble() * accuracy - accuracy / 2);
            //offsetY = (float)(rnd.NextDouble() * 3.0 - 1.5);
            if (attack.x > attack.y)
            {
                attack.x += attack.x * offset;

            }
            else
            {
                attack.y += attack.y * offset;
            }
            */
            attack.Normalize();
            Debug.Log(attack);
            Rigidbody2D attack2 = (Rigidbody2D)Instantiate(bullet, transform.position, transform.rotation);
            attack2.AddForce(attack * speed * 2.5f);
            //Debug.Log(attack);
            attack2.GetComponent<BasicRanged>().teamID = team;
            attack2.GetComponent<BasicRanged>().damage = 10;
            attack2.GetComponent<BasicRanged>().maxDistance = 30;
            reload = 1;
        }
        else
        {
            reload -= Time.fixedDeltaTime;
        }
        //rb2d.MovePosition(rb2d.position + curMove * speed  * Time.fixedDeltaTime);
        transform.rotation = Quaternion.AngleAxis((float)facing, Vector3.forward);
        lastLocation.first = id;
        lastLocation.second = face;

    }
    Vector2 getRoute()
    {
        System.Random rand = new System.Random();
        float angle = (float)rand.NextDouble() * (2 * Mathf.PI);
        facing = angle;
        double yMod, xMod;
        xMod = System.Math.Cos(angle);
        yMod = System.Math.Sin(angle);
        Vector2 position = new Vector2((float)xMod, (float)yMod);
        return position;
    }
    public float getDegree(float angle)
    {
        return (float)(angle * 180 / System.Math.PI);
    }
    void OnCollisionEnter2D(Collision2D collision)
    {
        route = false;


    }
    Vector2 getIntersection(Vector2 cur, Vector2 last, int spd)
    {
        Vector2 shoot;
        Vector2 target = new Vector2();
        Vector2 path = cur - last ;
        path.Normalize();
        path = path * spd;
        Debug.Log("Cur: " + cur + " LAST : " + last);
        Debug.Log("PATH: " + path);
        target.x = cur.x - rb2d.position.x;
        target.y = cur.y - rb2d.position.y;
        Debug.Log("target: " + target);
        float a = Vector2.Dot(path, path) - (this.speed * this.speed);
        Debug.Log("A:" + a);
        float b = 2 * Vector2.Dot(path, target);
        Debug.Log("B: " + b);
        float c = Vector2.Dot(target, target);
        Debug.Log("C: " + c);
        float p = -b / (2 * a);
        float q = (float)Mathf.Sqrt((b * b) - 4 * a * c) / (2 * a);
        Debug.Log("P:" + p + " Q " + q);
        float t1 = p - q;
        float t2 = p + q;
        float t;
        if(t1 > t2 && t2 > 0)
        {
            t = t2;
        }
        else
        {
            t = t1;
        }
        
        Vector2 aimSpot = cur + path * t;
        Debug.Log("Aim spot: " + aimSpot);
        Debug.Log(transform.position + " DIF " + rb2d.position);
        shoot.x = aimSpot.x - rb2d.position.x;
        shoot.y = aimSpot.y - rb2d.position.y;
       
        float timeToImpact = shoot.magnitude / this.speed;
        Debug.Log("Time: " + t + " Second Speed: " + timeToImpact);

        Debug.Log("Bullet End:" + (rb2d.position + shoot * this.speed * t));
        // shoot.Normalize();
        shoot.Normalize();
        Debug.Log("Returning shoot:" + shoot);
        return shoot;

    }
}
