using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class AffineTransform : MonoBehaviour
{
    [Header("Control Points (T0 is this.transform)")]
    [Tooltip("T1 - X-axis direction and scale")]
    public Transform T1;

    [Tooltip("T2 - Y-axis direction and scale")]
    public Transform T2;

    [Tooltip("T3 - Z-axis direction and scale")]
    public Transform T3;

    [Header("Debug")]
    [Tooltip("Show control points and connections in scene view")]
    public bool showGizmos = true;
    public float gizmoSize = 0.2f;

    [Header("Auto-Generated Hierarchy")]
    [Tooltip("Auto-created transform hierarchy for decomposed transformation")]
    public Transform translateTransform;      // T - Translation
    public Transform rotateTransform;         // R - Pure Rotation  
    public Transform reflectionTransform;     // F - Reflection
    public Transform shearRotateTransform;    // Q - Shear Rotation
    public Transform scaleTransform;          // D - Scale
    public Transform shearRotateInvTransform; // Qt - Inverse Shear Rotation

    private Matrix4x4 lastAffineMatrix;

    void Start()
    {
        CreateControlPointsIfNeeded();
        CreateHierarchyIfNeeded();
        UpdateTransformation();
    }

    void LateUpdate()
    {
        // Update transformation if control points have changed
        if (HasTransformationChanged())
        {
            UpdateTransformation();
        }
    }

    void OnValidate()
    {
        // Update in editor when values change
        if (Application.isPlaying || !Application.isEditor)
            return;

        CreateControlPointsIfNeeded();
        CreateHierarchyIfNeeded();
        UpdateTransformation();
    }

    void CreateControlPointsIfNeeded()
    {
        // Create T1 if it doesn't exist
        if (!T1)
        {
            GameObject t1GO = new GameObject("T1_X-Axis");
            t1GO.transform.SetParent(transform);
            t1GO.transform.localPosition = new Vector3(2, 0, 0);
            T1 = t1GO.transform;
        }

        // Create T2 if it doesn't exist
        if (!T2)
        {
            GameObject t2GO = new GameObject("T2_Y-Axis");
            t2GO.transform.SetParent(transform);
            t2GO.transform.localPosition = new Vector3(0, 2, 0);
            T2 = t2GO.transform;
        }

        // Create T3 if it doesn't exist
        if (!T3)
        {
            GameObject t3GO = new GameObject("T3_Z-Axis");
            t3GO.transform.SetParent(transform);
            t3GO.transform.localPosition = new Vector3(0, 0, 2);
            T3 = t3GO.transform;
        }
    }

    void CreateHierarchyIfNeeded()
    {
        // Create hierarchy: T → R → F → Q → D → Qt → (target object)
        if (!translateTransform)
        {
            GameObject translateGO = new GameObject("Translate_T");
            translateGO.transform.SetParent(transform);
            translateTransform = translateGO.transform;
        }

        if (!rotateTransform)
        {
            GameObject rotateGO = new GameObject("Rotate_R_Pure");
            rotateGO.transform.SetParent(translateTransform);
            rotateTransform = rotateGO.transform;
        }

        if (!reflectionTransform)
        {
            GameObject reflectionGO = new GameObject("Reflection_F");
            reflectionGO.transform.SetParent(rotateTransform);
            reflectionTransform = reflectionGO.transform;
        }

        if (!shearRotateTransform)
        {
            GameObject shearRotateGO = new GameObject("Rotate_Q_ShearAxis");
            shearRotateGO.transform.SetParent(reflectionTransform);
            shearRotateTransform = shearRotateGO.transform;
        }

        if (!scaleTransform)
        {
            GameObject scaleGO = new GameObject("Scale_D");
            scaleGO.transform.SetParent(shearRotateTransform);
            scaleTransform = scaleGO.transform;
        }

        if (!shearRotateInvTransform)
        {
            GameObject shearRotateInvGO = new GameObject("Rotate_Q_Transpose");
            shearRotateInvGO.transform.SetParent(scaleTransform);
            shearRotateInvTransform = shearRotateInvGO.transform;
        }
    }

    bool HasTransformationChanged()
    {
        if (!T1 || !T2 || !T3) return false;

        Matrix4x4 currentMatrix = BuildAffineMatrix();
        bool changed = currentMatrix != lastAffineMatrix;
        lastAffineMatrix = currentMatrix;
        return changed;
    }

    Matrix4x4 BuildAffineMatrix()
    {
        if (!T1 || !T2 || !T3) return Matrix4x4.identity;

        Vector3 t0 = Vector3.zero; // T0 is the origin in local space
        // Convert T1, T2, T3 positions to T0's local space regardless of their parenting
        Vector3 t1 = transform.InverseTransformPoint(T1.position);
        Vector3 t2 = transform.InverseTransformPoint(T2.position);
        Vector3 t3 = transform.InverseTransformPoint(T3.position);

        // Build the affine matrix M that maps a [0,1] unit cube to the target cuboid
        Matrix4x4 M = new Matrix4x4();
        M.SetColumn(0, new Vector4(t1.x - t0.x, t1.y - t0.y, t1.z - t0.z, 0));
        M.SetColumn(1, new Vector4(t2.x - t0.x, t2.y - t0.y, t2.z - t0.z, 0));
        M.SetColumn(2, new Vector4(t3.x - t0.x, t3.y - t0.y, t3.z - t0.z, 0));
        M.SetColumn(3, new Vector4(t0.x, t0.y, t0.z, 1));

        return M;
    }

    void UpdateTransformation()
    {
        if (!translateTransform || !rotateTransform || !reflectionTransform ||
            !shearRotateTransform || !scaleTransform || !shearRotateInvTransform)
            return;

        Matrix4x4 M = BuildAffineMatrix();

        // 1. Extract translation
        Vector3 T_translation = new Vector3(M.m03, M.m13, M.m23);

        // 2. Extract 3x3 linear part
        Matrix3x3 M_linear = new Matrix3x3(
            M.m00, M.m01, M.m02,
            M.m10, M.m11, M.m12,
            M.m20, M.m21, M.m22
        );

        // 3. Perform Polar Decomposition: M_linear = R_polar * S_polar
        Matrix3x3 MtM = Matrix3x3.Multiply(Matrix3x3.Transpose(M_linear), M_linear);
        JacobiEigendecomposition(MtM, out Matrix3x3 Q_mat, out Vector3 eigenvalues);

        Vector3 D_diag = new Vector3(
            Mathf.Sqrt(Mathf.Max(0, eigenvalues.x)),
            Mathf.Sqrt(Mathf.Max(0, eigenvalues.y)),
            Mathf.Sqrt(Mathf.Max(0, eigenvalues.z))
        );

        // S_polar_inverse = Q * D_inv * Q^T
        Vector3 D_inv_diag = new Vector3(
            D_diag.x == 0 ? 0 : 1.0f / D_diag.x,
            D_diag.y == 0 ? 0 : 1.0f / D_diag.y,
            D_diag.z == 0 ? 0 : 1.0f / D_diag.z
        );

        Matrix3x3 D_inv = Matrix3x3.Diagonal(D_inv_diag);
        Matrix3x3 S_inv = Matrix3x3.Multiply(Matrix3x3.Multiply(Q_mat, D_inv), Matrix3x3.Transpose(Q_mat));
        Matrix3x3 R_polar = Matrix3x3.Multiply(M_linear, S_inv);

        // 4. Decompose R_polar into pure rotation and reflection
        float R_det = R_polar.Determinant();
        Matrix3x3 R_pure = R_polar;
        Vector3 F_scale = Vector3.one;

        if (R_det < 0)
        {
            // R_polar includes reflection, extract it
            F_scale = new Vector3(-1, 1, 1); // Reflection in X
            // R_pure = R_polar * F_reflection_inverse
            Matrix3x3 reflectionMatrix = new Matrix3x3(-1, 0, 0, 0, 1, 0, 0, 0, 1);
            R_pure = Matrix3x3.Multiply(R_polar, reflectionMatrix);
        }

        // 5. Apply the full decomposed transformation T * R_pure * F * Q * D * Q^T

        // Apply Translation T
        translateTransform.localPosition = T_translation;
        translateTransform.localRotation = Quaternion.identity;
        translateTransform.localScale = Vector3.one;

        // Apply Pure Rotation R_pure
        rotateTransform.localPosition = Vector3.zero;
        rotateTransform.localRotation = MatrixToQuaternion(R_pure);
        rotateTransform.localScale = Vector3.one;

        // Apply Reflection F
        reflectionTransform.localPosition = Vector3.zero;
        reflectionTransform.localRotation = Quaternion.identity;
        reflectionTransform.localScale = F_scale;

        // Apply Shear Rotation Q
        shearRotateTransform.localPosition = Vector3.zero;
        shearRotateTransform.localRotation = MatrixToQuaternion(Q_mat);
        shearRotateTransform.localScale = Vector3.one;

        // Apply Scale D
        scaleTransform.localPosition = Vector3.zero;
        scaleTransform.localRotation = Quaternion.identity;
        scaleTransform.localScale = D_diag;

        // Apply Inverse Shear Rotation Q^T
        shearRotateInvTransform.localPosition = Vector3.zero;
        shearRotateInvTransform.localRotation = MatrixToQuaternion(Matrix3x3.Transpose(Q_mat));
        shearRotateInvTransform.localScale = Vector3.one;
    }

    Quaternion MatrixToQuaternion(Matrix3x3 m)
    {
        // Convert 3x3 rotation matrix to quaternion
        Matrix4x4 m4 = Matrix4x4.identity;
        m4.m00 = m[0, 0]; m4.m01 = m[0, 1]; m4.m02 = m[0, 2];
        m4.m10 = m[1, 0]; m4.m11 = m[1, 1]; m4.m12 = m[1, 2];
        m4.m20 = m[2, 0]; m4.m21 = m[2, 1]; m4.m22 = m[2, 2];

        return m4.rotation;
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // Draw T0 (this transform) as white sphere
        Gizmos.color = Color.white;
        Gizmos.DrawSphere(transform.position, gizmoSize);

        // Draw T1, T2, T3 if they exist
        if (T1)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(T1.position, gizmoSize);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, T1.position);
        }

        if (T2)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(T2.position, gizmoSize);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, T2.position);
        }

        if (T3)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(T3.position, gizmoSize);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, T3.position);
        }

        // Draw cuboid outline if all points exist
        if (T1 && T2 && T3)
        {
            Vector3 t0 = transform.position;
            Vector3 t1 = T1.position;
            Vector3 t2 = T2.position;
            Vector3 t3 = T3.position;

            Gizmos.color = Color.cyan;
            // Draw the cuboid edges
            Vector3[] corners = new Vector3[8]
            {
                t0,                    // 000
                t1,                    // 100
                t2,                    // 010
                t1 + t2 - t0,         // 110
                t3,                    // 001
                t1 + t3 - t0,         // 101
                t2 + t3 - t0,         // 011
                t1 + t2 + t3 - 2*t0   // 111
            };

            // Draw 12 edges of the cuboid
            int[,] edges = {
                {0,1}, {1,3}, {3,2}, {2,0}, // bottom face
                {4,5}, {5,7}, {7,6}, {6,4}, // top face
                {0,4}, {1,5}, {2,6}, {3,7}  // vertical edges
            };

            for (int i = 0; i < 12; i++)
            {
                Gizmos.DrawLine(corners[edges[i, 0]], corners[edges[i, 1]]);
            }
        }
    }

    void JacobiEigendecomposition(Matrix3x3 A, out Matrix3x3 V, out Vector3 eigenvalues)
    {
        V = Matrix3x3.Identity();
        Matrix3x3 D = A; // Copy

        int maxIterations = 50;
        float tolerance = 1e-10f;

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            // Find largest off-diagonal element
            int p = 0, q = 1;
            float maxVal = Mathf.Abs(D[p, q]);

            for (int r = 0; r < 3; r++)
            {
                for (int col = r + 1; col < 3; col++)
                {
                    if (Mathf.Abs(D[r, col]) > maxVal)
                    {
                        maxVal = Mathf.Abs(D[r, col]);
                        p = r;
                        q = col;
                    }
                }
            }

            if (maxVal < tolerance) break;

            // Calculate rotation angle
            float app = D[p, p];
            float aqq = D[q, q];
            float apq = D[p, q];
            float phi = 0.5f * Mathf.Atan2(2 * apq, app - aqq);
            float c = Mathf.Cos(phi);
            float s = Mathf.Sin(phi);

            // Create Jacobi rotation matrix
            Matrix3x3 J = Matrix3x3.Identity();
            J[p, p] = c; J[p, q] = -s;
            J[q, p] = s; J[q, q] = c;

            // Apply rotation: D = J^T * D * J
            D = Matrix3x3.Multiply(Matrix3x3.Multiply(Matrix3x3.Transpose(J), D), J);

            // Update eigenvector matrix: V = V * J
            V = Matrix3x3.Multiply(V, J);
        }

        eigenvalues = new Vector3(D[0, 0], D[1, 1], D[2, 2]);
    }
}

// Helper struct for 3x3 matrix operations
[System.Serializable]
public struct Matrix3x3
{
    public float m00, m01, m02;
    public float m10, m11, m12;
    public float m20, m21, m22;
    
    public Matrix3x3(float m00, float m01, float m02,
                     float m10, float m11, float m12,
                     float m20, float m21, float m22)
    {
        this.m00 = m00; this.m01 = m01; this.m02 = m02;
        this.m10 = m10; this.m11 = m11; this.m12 = m12;
        this.m20 = m20; this.m21 = m21; this.m22 = m22;
    }
    
    public float this[int row, int col]
    {
        get
        {
            return row switch
            {
                0 => col switch { 0 => m00, 1 => m01, 2 => m02, _ => 0 },
                1 => col switch { 0 => m10, 1 => m11, 2 => m12, _ => 0 },
                2 => col switch { 0 => m20, 1 => m21, 2 => m22, _ => 0 },
                _ => 0
            };
        }
        set
        {
            switch (row)
            {
                case 0:
                    switch (col)
                    {
                        case 0: m00 = value; break;
                        case 1: m01 = value; break;
                        case 2: m02 = value; break;
                    }
                    break;
                case 1:
                    switch (col)
                    {
                        case 0: m10 = value; break;
                        case 1: m11 = value; break;
                        case 2: m12 = value; break;
                    }
                    break;
                case 2:
                    switch (col)
                    {
                        case 0: m20 = value; break;
                        case 1: m21 = value; break;
                        case 2: m22 = value; break;
                    }
                    break;
            }
        }
    }
    
    public static Matrix3x3 Identity()
    {
        return new Matrix3x3(1, 0, 0, 0, 1, 0, 0, 0, 1);
    }
    
    public static Matrix3x3 Diagonal(Vector3 values)
    {
        return new Matrix3x3(values.x, 0, 0, 0, values.y, 0, 0, 0, values.z);
    }
    
    public static Matrix3x3 Multiply(Matrix3x3 a, Matrix3x3 b)
    {
        Matrix3x3 result = new Matrix3x3();
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                result[r, c] = a[r, 0] * b[0, c] + a[r, 1] * b[1, c] + a[r, 2] * b[2, c];
            }
        }
        return result;
    }
    
    public static Matrix3x3 Transpose(Matrix3x3 m)
    {
        return new Matrix3x3(m.m00, m.m10, m.m20,
                            m.m01, m.m11, m.m21,
                            m.m02, m.m12, m.m22);
    }
    
    public static Matrix3x3 Scale(Matrix3x3 m, float scale)
    {
        return new Matrix3x3(m.m00 * scale, m.m01 * scale, m.m02 * scale,
                            m.m10 * scale, m.m11 * scale, m.m12 * scale,
                            m.m20 * scale, m.m21 * scale, m.m22 * scale);
    }
    
    public float Determinant()
    {
        return m00 * (m11 * m22 - m12 * m21) -
               m01 * (m10 * m22 - m12 * m20) +
               m02 * (m10 * m21 - m11 * m20);
    }
}
