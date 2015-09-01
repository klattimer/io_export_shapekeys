// Unity editor plugin which detects shape key animations, and imports them



using UnityEngine;
using System;
using System.Collections;

[Serializable]
public class TextureReference : System.Object {
	public string name;
	public Texture2D texture;
}

[Serializable]
public class GameObjectReference : System.Object {
	public string name;
	public GameObject gameObject;
}

public class ShapeKeyImport : MonoBehaviour {
	public TextAsset dataFile;
	public TextureReference[] textureReferences;
	private Hashtable textureLookup;

	public GameObjectReference[] gameObjectReferences;
	private Hashtable gameObjectLookup;
	private JSONObject data;

	public void Awake() {
		textureLookup = new Hashtable ();
		for (int i = 0; i < textureReferences.Length; i++) {
			string name = textureReferences[i].name;
			if (name == "") {
				name = textureReferences[i].texture.name;
				textureReferences[i].name = name;
			}
			textureLookup.Add (textureReferences[i].name, textureReferences[i].texture);
		}
		gameObjectLookup = new Hashtable ();
		for (int i = 0; i < gameObjectReferences.Length; i++) {
			string name = gameObjectReferences[i].name;
			if (name == "") {
				name = gameObjectReferences[i].gameObject.name;
				gameObjectReferences[i].name = name;
			}
			gameObjectLookup.Add (gameObjectReferences[i].name, gameObjectReferences[i].gameObject);
		}
		string encodedString = dataFile.text;
		data = new JSONObject(encodedString);

		if (data.type == JSONObject.Type.OBJECT) {
			JSONObject shapeKeys = data["ShapeKeys"];
			if (shapeKeys.type == JSONObject.Type.OBJECT) {
				if (shapeKeys.list.Count > 0) {
					ShapeKeys sk = gameObject.AddComponent<ShapeKeys>() as ShapeKeys;
					ShapeKey[] shapekeyarray = new ShapeKey[shapeKeys.list.Count];
					for (int i = 0; i < shapeKeys.list.Count; i++) {
						JSONObject thisShapeKey = shapeKeys.list[i];
						JSONObject scaleVector = thisShapeKey["Scale"];

						ShapeKey s = new ShapeKey();
						s.name = shapeKeys.keys[i];
						s.meshObject = lookupGameObject(thisShapeKey["Object"].str);
						s.image = lookupTexture(thisShapeKey["ImageFile"].str);
						s.scale = new Vector3(scaleVector["x"].n, scaleVector["y"].n, scaleVector["z"].n);
						shapekeyarray[i] = s;
					}
					sk.shapeKeys = shapekeyarray;
					sk.InitialiseShapeKeys();
				}
			} else {
				Debug.Log ("shapeKeys: Should be an object!");
			}

			JSONObject animations = data["ShapeKeyAnimations"];
			if (animations.type == JSONObject.Type.OBJECT) {
				if (animations.list.Count > 0) {
					ShapeKeyAnimations ska = gameObject.AddComponent<ShapeKeyAnimations>() as ShapeKeyAnimations;
					ShapeKeyAnimation[] shapekeyanimarray = new ShapeKeyAnimation[animations.list.Count];
					for (int i = 0; i < animations.list.Count; i++) {
						JSONObject thisAnimation = animations.list[i];
						JSONObject frames = thisAnimation["Frames"];
						JSONObject keys = thisAnimation["ShapeKeys"];
						//JSONObject style = thisShapeKey["Style"];
						JSONObject startShape = thisAnimation["StartShape"];

						ShapeKeyAnimation anim = new ShapeKeyAnimation();
						anim.name = animations.keys[i];
						anim.numberOfFrames = frames.list.Count;
						anim.frameRate = thisAnimation["Framerate"].n;

						Hashtable shapeKeyFrames = new Hashtable();

						ShapeKey[] sks = new ShapeKey[keys.list.Count];
						for (int j = 0; j < keys.list.Count; j++) {
							ShapeKeys sk = gameObject.GetComponent<ShapeKeys>() as ShapeKeys;
							ShapeKey shapekey = sk.findShapeKeyNamed(keys[j].str);
							ShapeKeyFrameSequence sequence = new ShapeKeyFrameSequence();
							sequence.strength = new float[frames.list.Count];
							shapeKeyFrames.Add (shapekey, sequence);
							sks[j] = shapekey;
						}

						for (int j = 0; j < frames.list.Count; j++) {
							JSONObject frameData = frames[j];
							for (int k = 0; k < frameData.list.Count;k++) {
								JSONObject value = frameData[k];
								((ShapeKeyFrameSequence)shapeKeyFrames[sks[k]]).strength[j] = value.n;
							}
						}

						anim.frames = shapeKeyFrames;
						shapekeyanimarray[i] = anim;
					}
					ska.shapeKeyAnimations = shapekeyanimarray;
					ska.InitialiseAnimations();
				}
			} else {
				Debug.Log ("animations: Should be an object!");
			}
		} else {
			Debug.Log ("data: Should be an object!");
		}
	}

	public GameObject lookupGameObject(string name) {
		return (GameObject) gameObjectLookup[name];
	}

	public Texture2D lookupTexture(string name) {
		return (Texture2D) textureLookup[name];
	}
}
