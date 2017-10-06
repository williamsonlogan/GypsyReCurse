using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyController : MonoBehaviour {

	// Use this for initialization
	void Start () {
        
		Physics2D.IgnoreCollision(this.GetComponent<BoxCollider2D>(), GameObject.FindGameObjectWithTag("Player").GetComponent<BoxCollider2D>(), true);
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
