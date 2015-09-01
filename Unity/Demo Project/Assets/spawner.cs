using UnityEngine;
using System.Collections;

public class spawner : MonoBehaviour {
	public GameObject cube;


	// Use this for initialization
	void Start () {
		for (int i = 0; i < 10; i++) {
			for (int j = 0; j < 10; j++) {
				Instantiate(cube, new Vector3(-20 + (i * 4), 0, -20 + (j * 4)), new Quaternion());
			}
		}
	}
	
	// Update is called once per frame
	void Update () {
	
	}
}
