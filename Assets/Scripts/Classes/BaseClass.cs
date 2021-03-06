﻿using UnityEngine;
using System.Collections;
using SimpleJSON;
using System.Collections.Generic;
using System;

public abstract class BaseClass : MonoBehaviour {
    //Cooldowns
    public float[] cooldowns { get; protected set; }

    /* Base stats that all classes share*/
    protected PlayerBaseStat _classStat;

    public int team;
    public int playerID;
    private int yourPlayerID;
    private int allyKingID;
    private int enemyKingID;

    protected void Start ()
    {
        var networkingManager = GameObject.Find("GameManager").GetComponent<NetworkingManager>();
        yourPlayerID = GameManager.instance.player.GetComponent<BaseClass>().playerID;
        allyKingID = GameData.AllyKingID;
        enemyKingID = GameData.EnemyKingID;

        NetworkingManager.Subscribe(receiveAttackFromServer, DataType.Trigger, playerID);

        if (playerID == yourPlayerID)
        {
            HUD_Manager.instance.subSkill.CoolDown = cooldowns[0];
            HUD_Manager.instance.mainSkill.CoolDown = cooldowns[1];
            HUD_Manager.instance.playerProfile.Health.fillAmount = ClassStat.CurrentHp / ClassStat.MaxHp;
            if (playerID == allyKingID)
                HUD_Manager.instance.allyKing.Health.fillAmount = ClassStat.CurrentHp / ClassStat.MaxHp;
            if (playerID == enemyKingID)
                HUD_Manager.instance.enemyKing.Health.fillAmount = ClassStat.CurrentHp / ClassStat.MaxHp;
        }

        _classStat = new PlayerBaseStat(playerID);
    }

	public PlayerBaseStat ClassStat
	{
		get
        {
            if (this._classStat == null)
            {
                this._classStat = new PlayerBaseStat(playerID);
            }
            return this._classStat;
        }

		protected set
		{
			this._classStat.CurrentHp = value.CurrentHp;
			this._classStat.MaxHp = value.MaxHp;
			this._classStat.MoveSpeed = value.MoveSpeed;
			this._classStat.AtkPower = value.AtkPower;
            this._classStat.Defense = value.Defense;
		}
	}

    public float doDamage(float damage, bool trueDamage = false)
    {
        // hank: Added defensive calculation
        float finaldamage = damage;

        if (!trueDamage)
        {
            float reduction = (30 / (ClassStat.Defense + 30));
            finaldamage = damage * reduction;
        }
        
        ClassStat.CurrentHp -= finaldamage;
        if(ClassStat.CurrentHp > ClassStat.MaxHp)
        {
            ClassStat.CurrentHp = ClassStat.MaxHp;
        }

        //Debug.Log(ClassStat.CurrentHp + "/" + ClassStat.MaxHp + " HP");

        GameManager.instance.PlayerTookDamage(playerID, finaldamage, ClassStat);

        if (ClassStat.CurrentHp <= 0.0f)
        {
            //death
            NetworkingManager.Unsubscribe(DataType.Player, playerID);
            Destroy(gameObject);
        }

        return finaldamage;
    }

    void OnTriggerEnter2D(Collider2D other) {
        Trigger attack;
        if ((attack = other.gameObject.GetComponent<Trigger>()) != null) {
            if (attack.teamID == team || GameData.MyPlayer == null) {
                return;
            }

            var damageTaken = 0f;
            if (playerID == GameData.MyPlayer.PlayerID)
                damageTaken = doDamage(attack.damage);

            if (GameData.MyPlayer == null || playerID != GameData.MyPlayer.PlayerID)
                return;

            var memersToSend = new List<Pair<string, string>>();
            memersToSend.Add(new Pair<string, string>("EnemyID", attack.playerID.ToString()));
            memersToSend.Add(new Pair<string, string>("NewHP", ClassStat.CurrentHp.ToString()));
            print(NetworkingManager.send_next_packet(DataType.Hit, GameData.MyPlayer.PlayerID, memersToSend, Protocol.UDP));
            
            return;
        } else {
            Debug.Log("Attack was null");
        }
    }

    void receiveAttackFromServer(JSONClass playerData)
    {
        if (playerData["ID"].AsInt == GameData.MyPlayer.PlayerID)
            return;
        Vector2 directionOfAttack = new Vector2(playerData["DirectionX"].AsFloat, playerData["DirectionY"].AsFloat);
        switch (playerData["Attack"].AsInt)
        {
            case 0:
                basicAttack(directionOfAttack);
                //Regular attack
                break;
            case 1:
                specialAttack(directionOfAttack);
                //Regular special attack
                break;
            case 2:
                //Aman special attack
                break;
            default:
                break;
        }
    }

    public virtual float basicAttack(Vector2 dir)
    {
        return cooldowns[0];
    }

    public virtual float specialAttack(Vector2 dir)
    {
        return cooldowns[1];
    }

    [System.Serializable]
	public class PlayerBaseStat
	{
        public PlayerBaseStat(int id)
        {
            _playerID = id;
        }

        private int _playerID;
        private float _currentHp;
		public float CurrentHp {
            get
            {
                return _currentHp;
            }
            set {
                _currentHp = (value > MaxHp) ? MaxHp : value;
            }
        }
		public float MaxHp;
		public float MoveSpeed;
        private float _atkPower;
		public float AtkPower
        {
            get { return _atkPower; }
            set
            {
                _atkPower = value;
                update_stats();
            }
        }

        private float _defense;
        public float Defense
        {
            get { return _defense; }
            set
            {
                _defense = value;
                update_stats();
            }
        }

        public void update_stats()
        {
            if (_playerID != GameData.MyPlayer.PlayerID)
                return;
            List<Pair<string, string>> memers = new List<Pair<string, string>>();
            memers.Add(new Pair<string, string>("AtkPower", AtkPower.ToString()));
            memers.Add(new Pair<string, string>("Defense", Defense.ToString()));
            //NetworkingManager.send_next_packet(DataType.StatUpdate, _playerID, memers, Protocol.TCP));
            Debug.Log(NetworkingManager.send_next_packet(DataType.StatUpdate, _playerID, memers, Protocol.TCP));
        }
    }

    public void StartAttackAnimation()
    {
        gameObject.GetComponent<Animator>().SetBool("attacking", true);
    }

    public void EndAttackAnimation()
    {
        gameObject.GetComponent<Animator>().SetBool("attacking", false);
    }

    public void UsePotion(int damage, int armour, int health, int speed, int duration)
    {
        if (duration == 0)
        {
            ClassStat.AtkPower += damage;
            ClassStat.Defense += armour;
            if (health != 0)
                doDamage(-health);
            ClassStat.CurrentHp += health;
            ClassStat.MoveSpeed += speed;
        } else
        {
            if (damage != 0)
                ClassStat.AtkPower += damage;
            if (armour != 0)
                ClassStat.Defense += armour;
            if (health != 0)
                doDamage(-health);
            ClassStat.CurrentHp += health;
            ClassStat.MoveSpeed += speed;
            Debug.Log(ClassStat.MoveSpeed);
            StartCoroutine(Debuff(damage, armour, speed, duration));
        }
    }

    IEnumerator Debuff(int damage, int armour, int speed, int duration)
    {
        yield return new WaitForSeconds(duration);
        if (damage != 0)
            ClassStat.AtkPower -= damage;
        if (armour != 0)
            ClassStat.Defense -= armour;
        ClassStat.MoveSpeed -= speed;
    }
}
