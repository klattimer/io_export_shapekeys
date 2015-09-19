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
	reset,
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
			// We calculate the current time based on distance from the end,
			// rather than distance from the start, this helps us deal with
			// pausing the animation.
            return duration - (_endTime - _currentTime);
        }
        set {
            // set the time, next update will set the verts
			_currentTime = value;
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
            return (int)Mathf.Floor(currentTime / duration);
        }
        set {
            // Move the animation to the specified frame as a time index.
			currentTime = value * frameDelay;
        }
    }


    private AnimationState animationState = AnimationState.stopped;
    public AnimationStyle animationStyle = AnimationStyle.end;
	public ShapeKeyAnimationDelegate delegateInterface;

	private bool needsRedraw = false;
    private bool isReversing;
    private Hashtable meshObjects;
    private Hashtable basisMeshes;

    public Hashtable frames;
	public Hashtable startShapes;

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
                shapeKey.strength = shapeKeyStrengthAtTime(shapeKey, currentTime);
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
		foreach (DictionaryEntry de in meshObjects) {
			GameObject meshObject = (GameObject)de.Key;
			List<ShapeKey> s = (List<ShapeKey>)de.Value;
			// Get the basis mesh
			Vector3[] mesh = (Vector3[])((VertexContainer) basisMeshes[meshObject]).vertices.Clone();
			for (int i = 0; i < s.Count; i++) {
				ShapeKey shapeKey = s[i];
				if (!startShapes.ContainsKey(shapeKey))
					continue;
				shapeKey.strength = (float)startShapes[shapeKey];
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
        if (animationState == AnimationState.playing || needsRedraw) {
			if (isReversing) {
				_currentTime -= Time.deltaTime;
			} else {
            	_currentTime += Time.deltaTime;
			}
			if (_currentTime >= _endTime) {
                _currentTime = _endTime;
                // Animation ended, Based on animation style work out what to do next
				if (animationStyle == AnimationStyle.loop) {
					_currentTime = 0;
				} else if (animationStyle == AnimationStyle.pingPong) {
					isReversing = !isReversing;
				} else if (animationStyle == AnimationStyle.reset) {
					Reset();
					animationState = AnimationState.stopped;
				} else if (animationStyle == AnimationStyle.end) {
					animationState = AnimationState.stopped;
				}
            }

            accumulate();
        }
    }
}

public interface ShapeKeyAnimationDelegate {
	void animationEnded(string animationName, GameObject target);
	void animationPlaying(string animationName, GameObject target);
	void animationPaused(string animationName, GameObject target);
	void animationLoop(string animationName, GameObject target);
	void animationReverse(string animationName, GameObject target);
}

[RequireComponent (typeof (ShapeKeys))]
public class ShapeKeyAnimations : MonoBehaviour {
    public ShapeKeyAnimation[] shapeKeyAnimations;
	private Hashtable shapekeyanimationlookup;

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
