using UnityEngine;
using System.Collections;

//[RequireComponent (typeof (ShapeKeys))]
public class CubeMorphTest : MonoBehaviour {
	private float popdelay = 2.0f;

	// Use this for initialization
	void Start () {
		/*ShapeKeys sk = (ShapeKeys)GetComponent ("ShapeKeys");
		if (sk) {
			Vector3[] verts = sk.shapeKeys[0].getVertices();
			
			sk.shapeKeys[0].strength = 1.0f;
			verts = sk.shapeKeys[0].applyShapeToVertices(verts);
			Mesh m = sk.shapeKeys[0].getMesh();
			m.vertices = verts;
		}*/

	}
	
	// Update is called once per frame
	void Update () {
		popdelay -= Time.deltaTime;

		if (popdelay <= 0) {
			popdelay = 5.0f;
			
			ShapeKeyAnimations shapeKeyAnims = gameObject.GetComponent<ShapeKeyAnimations>();
			shapeKeyAnims.shapeKeyAnimations [0].Play ();
		}
	

	}
}
