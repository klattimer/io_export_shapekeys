using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

// TODO: Editor UI & Features
// - Play animation
// - Set frame of animation
// - Set time of animation

public enum AnimationState {
	stopped,
	playing,
	paused
}

public enum AnimationStyle {
	end,
	freeze,
	loop,
	pingPong
}

[Serializable]
public class ShapeKeyFrameSequence : System.Object {
	public float[] strength;
}

[Serializable]
public class ShapeKeyAnimation : System.Object {
	public string name;
    public ShapeKeyAnimations parent;

    public ShapeKeys    shapeKeys;
    public int          numberOfFrames = 0;
	public float		frameRate = 24.0f;
	public float        frameDelay
	{
		get {
			return 1.0f/frameRate;
		}
	}

    public float        duration {
        get {
            return numberOfFrames * frameDelay;
        }
    }

    private float        _startTime;
    public float        startTime {
        get {
            return _startTime;
        }
    }
    private float       _currentTime;
    public float        currentTime {
        get {
            return _currentTime;
        }
        set {
            // Move the animation to the specified frame as a time since level load
        }
    }

    private float       _endTime;
    public float        endTime {
        get {
            return _endTime;
        }
    }

    public bool         hasEnded {
        get {
            return (currentTime >= endTime);
        }
    }

    public int          currentFrame {
        get {
            return (int)Mathf.Floor(duration - (_endTime - _currentTime) / duration);
        }
        set {
            // Move the animation to the specified frame as a time index.
        }
    }


    private AnimationState animationState = AnimationState.stopped;
    public AnimationStyle animationStyle = AnimationStyle.end;

    private bool isReversing;
    private Hashtable meshObjects;
    private Hashtable basisMeshes;

    public Hashtable frames;
	
    private float shapeKeyStrengthAtTime(ShapeKey shapeKey, float time) {
		int frameForTime = ((int)Mathf.Floor((time / duration) * (float)(numberOfFrames - 1)));

		ShapeKeyFrameSequence f = (ShapeKeyFrameSequence)frames[shapeKey];
		
		if (f == null)
			return 0;

        if (frameForTime >= numberOfFrames - 1) {
			return f.strength[numberOfFrames - 1];
		}

        // Get the frame
		float l = f.strength[frameForTime];
		float h = f.strength[frameForTime + 1];
        float d = l - h;

		float lt = (float)frameForTime * frameDelay;
		float ht = ((float)frameForTime + 1.0f) * frameDelay;

		float dt = lt - ht;
		float s = (time - lt) / dt;

		float r = l + (d * s);
		/*if (r < 0)
			r = 0;
		if (r > 1)
			r = 1;
		*/
		return r;
    }

    public void Play() {
        if (animationState == AnimationState.stopped) {
            _startTime = Time.timeSinceLevelLoad;
            _currentTime = _startTime;
            _endTime = _startTime + duration;
            animationState = AnimationState.playing;
            Reset();
        } else if (animationState == AnimationState.paused) {
            float r = _endTime - _currentTime;
            _endTime = Time.timeSinceLevelLoad + r;
            _currentTime = Time.timeSinceLevelLoad;
            animationState = AnimationState.playing;
        }
    }

    private void accumulate() {
		foreach (DictionaryEntry de in meshObjects) {
			GameObject meshObject = (GameObject)de.Key;
			List<ShapeKey> s = (List<ShapeKey>)de.Value;
            // Get the basis mesh
			Vector3[] mesh = (Vector3[])((VertexContainer) basisMeshes[meshObject]).vertices.Clone();
            for (int i = 0; i < s.Count; i++) {
                ShapeKey shapeKey = s[i];
                shapeKey.strength = shapeKeyStrengthAtTime(shapeKey, duration - (_endTime - _currentTime));
				mesh = shapeKey.applyShapeToVertices(mesh);
            }

			Mesh m;
			
			if (meshObject.GetComponent("SkinnedMeshRenderer") == null) {
				MeshFilter f = (MeshFilter) meshObject.GetComponent("MeshFilter");
				m = f.mesh;
			} else {
				MeshFilter f = (MeshFilter) meshObject.GetComponent("SkinnedMeshRenderer");
				m = f.sharedMesh;
			}
			
			m.vertices = mesh;
		}
    }

    public void Pause() {
        animationState = AnimationState.paused;
    }

    public void Stop() {
        animationState = AnimationState.stopped;
    }

    // *ALWAYS* returns the states of the shape keys to FRAME ZERO
    // This may, if any of the shape keys are applied result in a deformed
    // mesh, a backup should always be retained.
    public void Reset() {

    }

	private Vector3[] cloneBaseMesh(Vector3[] vertices) {
		Vector3[] output = new Vector3[vertices.Length];
		for (int i = 0; i < vertices.Length; i++) {
			output[i] = new Vector3(vertices[i].x, vertices[i].y, vertices[i].z);
		}
		return output;
	}

    public void Start() {
		meshObjects = new Hashtable ();
		basisMeshes = new Hashtable ();
        for (int i = 0; i < shapeKeys.shapeKeys.Length; i++) {
            // Lookup the mesh and collect the list of shape keys for each mesh
            // Also grab a copy of the basis mesh so we always apply values
			// against the basis.
			GameObject meshObject = ((ShapeKey)shapeKeys.shapeKeys[i]).meshObject;
            if (!meshObjects.Contains(meshObject)) {
                List<ShapeKey> s = new List<ShapeKey>();
                s.Add(shapeKeys.shapeKeys[i]);

                VertexContainer verts = new VertexContainer();
				verts.vertices =  (Vector3[])((ShapeKey)shapeKeys.shapeKeys[i]).getVertices().Clone();
                meshObjects.Add(meshObject, s);
                basisMeshes.Add(meshObject, verts);
            } else {
				((List<ShapeKey>)meshObjects[meshObject]).Add(shapeKeys.shapeKeys[i]);
            }
        }
    }

    public void Update() {
        if (animationState == AnimationState.playing) {
            _currentTime += Time.deltaTime;
            
			if (_currentTime >= _endTime) {
                _currentTime = _endTime;
				animationState = AnimationState.stopped;
                // TODO: Animation ended

                // Based on animation style work out what to do next
            }

            accumulate();
        }
    }
}

[RequireComponent (typeof (ShapeKeys))]
public class ShapeKeyAnimations : MonoBehaviour {
    public ShapeKeyAnimation[] shapeKeyAnimations;
	private Hashtable shapekeyanimationlookup;

    public Signal animationEnded;
    public Signal animationPlaying;
    public Signal animationPaused;
    public Signal animationLoop;
    public Signal animationReverse;

    public ShapeKeyAnimation findAnimationNamed(string name) {
		return (ShapeKeyAnimation)shapekeyanimationlookup [name];
    }

	public void InitialiseAnimations() {
		shapekeyanimationlookup = new Hashtable ();
        for (int i = 0; i < shapeKeyAnimations.Length; i++) {
			shapekeyanimationlookup.Add (shapeKeyAnimations[i].name, shapeKeyAnimations[i]);
            shapeKeyAnimations[i].parent = this;
            shapeKeyAnimations[i].shapeKeys = (ShapeKeys)GetComponent("ShapeKeys");
			shapeKeyAnimations[i].Start();
        }
    }

    public void Update() {
        for (int i = 0; i < shapeKeyAnimations.Length; i++) {
            shapeKeyAnimations[i].Update();
        }
    }
}
