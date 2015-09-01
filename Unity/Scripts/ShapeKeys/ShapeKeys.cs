using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// TODO: Editor UI & Features
// - Record a frame
// - Set shape strength for testing (slider?)


public class VertexContainer {
	public Vector3[] vertices;
}
/*
[CustomEditor(typeof(ShapeKeys))]
public class ShapeKeysEditor : Editor {
    private Hashtable meshObjects;
    private Hashtable basisMeshes;

    public override void OnInspectorGUI() {
        base.OnInspectorGUI();
        ShapeKeys shapeKeys = (ShapeKeys)target;

        if (GUILayout.Button("RecordFrame")) {
        }

        for (int i = 0; i < shapeKeys.shapeKeys.Length; i++) {
            float v = EditorGUILayout.Slider(shapeKeys.shapeKeys[i].name, shapeKeys.shapeKeys[i].strength, 0.0f, 1.0f);
            if (shapeKeys.shapeKeys[i].strength != v) {
                Undo.RecordObject(shapeKeys, shapeKeys.shapeKeys[i].name + " strength was changed");
                shapeKeys.shapeKeys[i].strength = v;
                EditorUtility.SetDirty(shapeKeys);
            }
        }
    }

    private void accumulate() {
        foreach (GameObject meshObject in meshObjects) {
            Vector3[] mesh = ((V) basisMeshes[meshObject]).vertices;
            for (int i = 0; i < meshObjects[meshObject]; i++) {
                mesh = shapeKey.applyShapeToVertices(mesh);
            }
            meshObject.mesh.vertices = mesh;
        }
    }

    private void reset() {
        foreach (GameObject meshObject in meshObjects) {
            Vector3[] mesh = ((VertexContainer) basisMeshes[meshObject]).vertices;
            meshObject.mesh.vertices = mesh;
        }
    }
}
*/
[Serializable]
public class ShapeKey : System.Object {
	public string name;
	// Strength of the applied shape
	private float       _strength;
	public float        strength {
		get {
			return _strength;
		}
		set {
			_strength = value;
			meshChanged = true;
		}
	}
	
	// Scale of the applied shape
	// This is the relative scale between the changes in each x,y,z
	// For instance if the x change was 1 and the y change was 1
	// and the scale was x = 1, y = 2 then the the output would be
	// 1,2 - scale is used to denote the base-relative-scale of the
	// morph, so that (rgb * strength * scale) + originalvertex = vertex
	public Vector3     scale;
	
	// Image of Shape Keys encoded to UVs
	public Texture2D    image;
	
	// The mesh the shape applies to
	public GameObject   meshObject;
	
	// UV Layer to use for shape keys (usually 0, sometimes 1)
	public int          UVLayer = 0;
	
	// Recalculate normals on every frame
	public bool         alwaysRecalculateNormals = false;
	
	// Only apply the shape key when the object is visible;
	public bool         onlyWhenVisible = true;
	
	private Vector3[]   _shapes;
	private Vector3[]   _shapesApplied;
	public Vector3[]    shapes {
		get {
			//if (onlyWhenVisible && !meshObject.) return;
			
			if (meshChanged == false) return _shapesApplied;
			
			// For external use we return the shapes multiplied by the current
			// strength
			Vector3[] output = new Vector3[_shapes.Length];
			for (int i = 0; i < output.Length; i++) {
				output[i] = _shapes[i] * strength;
			}
			_shapesApplied = output;
			meshChanged = false;
			return _shapesApplied;
		}
	}
	
	private bool        meshChanged = false;
	private float       textureWidth;
	private float       textureHeight;
	
	// Our "seam" collection
	private List<List<int>> vertexCollection;
	
	private Vector3 colorToVertexShift(Color color) {
		Vector3 cv = new Vector3();

		cv.x = ((color.r * 2.0f) - 1.0f) * scale.x;
		cv.y = (((color.g * 2.0f) - 1.0f) * scale.y) * -1.0f;
		cv.z = ((color.b * 2.0f) - 1.0f) * scale.z;
		return cv;
	}
	
	private Vector3 valueFor(Vector2 uv) {
		if (image == null) {
			Debug.Log("No image");
		}

		// Get the RGB value for u,v ~= x,y on the attached diffmap
		Color pixel = image.GetPixel((int)Mathf.Round (uv.x * textureWidth), 
		                             (int)Mathf.Round (uv.y * textureHeight));
		// We take that value and multiply it by scale to create our "shape"
		// vector, and store that in an array of vectors with the same indices
		// as the basis mesh.
		return colorToVertexShift(pixel);
	}
	
	private void loadSeams() {
		Vector3[] basis = getVertices ();
		
		// Find clusters of vertices which are connected at a seam
		// all vertices in these clusters should always have the same x,y,z
		// position
		for ( int vert = 0 ; vert < basis.Length ; vert++ ) {
			List<int> indicesOfCollection = new List<int>();
			indicesOfCollection.Add(vert);
			for ( int findvert = 0; findvert < basis.Length ; findvert++ ) {
				if (findvert == vert) continue;
				
				if (basis[vert] == basis[findvert]) {
					indicesOfCollection.Add(findvert);
				}
			}
			vertexCollection.Add(indicesOfCollection);
		}
	}

	public Vector3[] getVertices() {
		return getMesh ().vertices;
	}

	public Mesh getMesh() {
		Mesh mesh;
		if (meshObject.GetComponent("SkinnedMeshRenderer") == null) {
			MeshFilter f = (MeshFilter) meshObject.GetComponent("MeshFilter");
			mesh = f.mesh;
		} else {
			MeshFilter f = (MeshFilter) meshObject.GetComponent("SkinnedMeshRenderer");
			mesh = f.sharedMesh;
		}
		return mesh;
	}

	private void updateShapes() {
		loadSeams();
		textureWidth = image.width;
		textureHeight = image.height;
		Mesh mesh;
		if (meshObject.GetComponent("SkinnedMeshRenderer") == null) {
			MeshFilter f = (MeshFilter) meshObject.GetComponent("MeshFilter");
			mesh = f.mesh;
		} else {
			MeshFilter f = (MeshFilter) meshObject.GetComponent("SkinnedMeshRenderer");
			mesh = f.sharedMesh;
		}

		Vector2[] uvs;
		if (UVLayer == 0) {
			uvs = mesh.uv;
		} else {
			uvs = mesh.uv2;
		}
		
		// Iterate over the UVs, decode the mesh positions from the UVs
		// Generate an array of "shapes". Each shape will be summed up
		// when applied.
		
		// The indices of the UVs should match the indices of the vertices
		// That are in the base mesh.
		Vector3[] outShapes = new Vector3[uvs.Length];
		bool[] done = new bool[uvs.Length];
		for (int i = 0; i < uvs.Length; i++) {
			done[i] = false;
		}
		for (int i = 0; i < uvs.Length; i++) {
			if (done[i] == true) continue;
			
			// Grab the shape value and pop it into the array.
			List<int> correlatingVerts = vertexCollection[i];
			if (correlatingVerts.Count == 1) {
				// When it's just a single vert on it's own, we can do it
				// directly.
				outShapes[i] = valueFor(uvs[i]);
				done[i] = true;
			} else {
				Vector3 sum = new Vector3(0,0,0);
				// When there is more than one vertex at a particular point
				// we assume that the UVs are the *same* colour, however
				// they probably have some variation due to being encoded
				// as baked textures. To combat this we sum the vertex values
				// and take the average.
				// This way we avoid trying to re-merge them later.
				for (int j = 0; j < correlatingVerts.Count; j++) {
					sum += valueFor(uvs[correlatingVerts[j]]);
				}
				sum /= correlatingVerts.Count;
				for (int j = 0; j < correlatingVerts.Count; j++) {
					if (done[correlatingVerts[j]] == true) continue;
					outShapes[correlatingVerts[j]] = sum;
					done[correlatingVerts[j]] = true;
				}
			}
		}
		_shapes = outShapes;
		// At this point, if we're in the game we can discard our reference
		// to the image and the garbage collector should destroy it and free
		// the memory, dispose of anything else that's now not required.
		image = null;
	}
	
	public Vector3[] applyShapeToVertices(Vector3[] verts) {
		// this.shapes iterates and multiplies out the current strength
		// therefore, we take a cache of it to avoid iterating and doing
		// too often.
		Vector3[] localShapes = shapes;
		for (int i = 0; i < verts.Length; i++) {
			verts[i].x += localShapes[i].x;
			verts[i].y += localShapes[i].y;
			verts[i].z += localShapes[i].z;
		}
		return verts;
	}
	
	public void Start() {
		vertexCollection = new List<List<int>>();
		updateShapes();
	}
	
	public void Update() {
		// Nothing for us to update as we're just relying on the
		// owner class and adjacent classes to manage our strength.
	}
}


public class ShapeKeys : MonoBehaviour {
	
	// Verbosity of output
	public int          verbose = 0;
	
	// The ShapeKey is ready once it's parsed over the texture and UVs
	// to produce the output mesh deformations.
	public bool         isReady = false;
    

    public ShapeKey[] shapeKeys;
	private Hashtable shapekeylookup;

    public ShapeKey findShapeKeyNamed(string shapeKeyName) {
		return (ShapeKey)shapekeylookup [shapeKeyName];
    }

	public void InitialiseShapeKeys() {
		shapekeylookup = new Hashtable ();
		Debug.Log ("Initialising shape keys");
		for (int i = 0; i < shapeKeys.Length; i++) {
			shapekeylookup.Add (shapeKeys[i].name, shapeKeys[i]);
			shapeKeys [i].Start ();
		}
		isReady = true;
	}
}
