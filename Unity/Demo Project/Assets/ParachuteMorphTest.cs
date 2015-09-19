using UnityEngine;
using System.Collections;

public class ParachuteMorphTest : MonoBehaviour {
	float popdelay = 3.0f;
	// Use this for initialization
	void Start () {
		
		ShapeKeyAnimations shapeKeyAnims = gameObject.GetComponent<ShapeKeyAnimations>();
		shapeKeyAnims.shapeKeyAnimations [0].Reset ();
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
