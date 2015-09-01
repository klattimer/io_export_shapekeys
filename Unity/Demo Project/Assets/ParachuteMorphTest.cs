using UnityEngine;
using System.Collections;

public class ParachuteMorphTest : MonoBehaviour {
	float popdelay = 2.2f;
	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
		popdelay -= Time.deltaTime;
		
		if (popdelay <= 0) {
			popdelay = 2.2f;
			
			ShapeKeyAnimations shapeKeyAnims = gameObject.GetComponent<ShapeKeyAnimations>();
			shapeKeyAnims.shapeKeyAnimations [0].Play ();
		}

	}
}
