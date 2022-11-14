using AOT;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public class StereoNerfRenderer : MonoBehaviour
{


    public int width;
    public int height;
    public bool enableDlss;
    public Material leftMaterial, rightMaterial;

    public string nerf_path = "Z:/Code/vrnerf/instant-ngp/data/nerf/fox";
    public Camera XRELeftEyeCamera;
    public float IPD = 0.063f; // here we use the average human IPD.


    // aabb cropping settings
    // TODO: enable dynamics AABB cropping
    Vector3 aabb_min = new Vector3(-1.5f, -1.5f, -1.5f);
    Vector3 aabb_max = new Vector3(2.5f, 2.5f, 2.5f);
    static bool has_aabb_updated = true;

    // material & texture management
    Texture2D leftTexture, rightTexture;
    Transform XRRightEyeTransform;
    static System.IntPtr leftHandle, rightHandle;
    static int _width, _height;

    static bool already_initalized = false;
    static bool handle_changed = false;

    // camera view and transform management
    static Vector3 left_forwardPos;
    static Vector3 left_upPos;
    static Vector3 left_rightPos;
    static Vector3 left_positionPos;

    static Vector3 right_forwardPos;
    static Vector3 right_upPos;
    static Vector3 right_rightPos;
    static Vector3 right_positionPos;

    /// <summary> Renders the event delegate described by eventID. </summary>
    /// <param name="eventID"> Identifier for the event.</param>
    private delegate void RenderEventDelegate(int eventID);
    /// <summary> Handle of the render thread. </summary>
    private static RenderEventDelegate RenderThreadHandle = new RenderEventDelegate(RunOnRenderThread);
    /// <summary> The render thread handle pointer. </summary>
    public static System.IntPtr RenderThreadHandlePtr = Marshal.GetFunctionPointerForDelegate(RenderThreadHandle);

    public const int INIT_EVENT = 0x0001;
    public const int DRAW_EVENT = 0x0002;
    public const int DEINIT_EVENT = 0x0003;

    /// <summary> Executes the 'on render thread' operation. </summary>
    /// <param name="eventID"> Identifier for the event.</param>
    [MonoPInvokeCallback(typeof(RenderEventDelegate))]
    private static void RunOnRenderThread(int eventID) {

        switch (eventID)
        {
            case INIT_EVENT:
                leftHandle = NerfRendererPlugin.create_texture(_width, _height);
                rightHandle = NerfRendererPlugin.create_texture(_width, _height);
                handle_changed = true;
                break;

            case DRAW_EVENT:


                float[] camera_matrix_LEFT = new float[3 * 4] {
                    left_rightPos.x,    left_rightPos.y,    left_rightPos.z,
                    left_upPos.x,       left_upPos.y,       left_upPos.z,
                    left_forwardPos.x,  left_forwardPos.y,  left_forwardPos.z,
                    left_positionPos.x, left_positionPos.y, left_positionPos.z
                };

                float[] camera_matrix_RIGHT = new float[3 * 4] {
                    right_rightPos.x,    right_rightPos.y,    right_rightPos.z,
                    right_upPos.x,       right_upPos.y,       right_upPos.z,
                    right_forwardPos.x,  right_forwardPos.y,  right_forwardPos.z,
                    right_positionPos.x, right_positionPos.y, right_positionPos.z
                };

                NerfRendererPlugin.update_texture(camera_matrix_LEFT, leftHandle);
                NerfRendererPlugin.update_texture(camera_matrix_RIGHT, rightHandle);
                break;

            case DEINIT_EVENT:

                NerfRendererPlugin.destroy_texture(leftHandle);
                NerfRendererPlugin.destroy_texture(rightHandle);
                NerfRendererPlugin.deinitialize();

                already_initalized = false;

                break;
        }
    }

    void OnEnable() {       

        if (already_initalized)
            return;

        already_initalized = true;
        if (!Directory.Exists(nerf_path) || File.Exists(nerf_path + "/base.msgpack")) {
            Debug.LogWarning(nerf_path + " not found");
        }

        NerfRendererPlugin.initialize(nerf_path, nerf_path + "/base.msgpack", enableDlss);
        _width = width;
        _height = height;


        GL.IssuePluginEvent(RenderThreadHandlePtr, INIT_EVENT);
        GL.InvalidateState();
    }

    void OnDestroy()
    {

        GL.IssuePluginEvent(RenderThreadHandlePtr, DEINIT_EVENT);
        GL.InvalidateState();
        Debug.Log("Deinitialize Texture");

        // TODO: cleanup native plugin properly.
        // https://answers.unity.com/questions/1425847/native-plugin-cleanup.html
    }

    void Update()
    {
        left_positionPos = XRELeftEyeCamera.transform.position;
        left_forwardPos = XRELeftEyeCamera.transform.forward;
        left_upPos = XRELeftEyeCamera.transform.up;
        left_rightPos = XRELeftEyeCamera.transform.right;

        XRRightEyeTransform = XRELeftEyeCamera.transform;
        XRRightEyeTransform.position = new Vector3(XRELeftEyeCamera.transform.position.x - IPD, XRELeftEyeCamera.transform.position.y, XRELeftEyeCamera.transform.position.z);
        right_positionPos = XRRightEyeTransform.transform.position;
        right_forwardPos = XRRightEyeTransform.transform.forward;
        right_upPos = XRRightEyeTransform.transform.up;
        right_rightPos = XRRightEyeTransform.transform.right;
        
        // TODO: enable dynamics AABB cropping
        if (!has_aabb_updated)
        {
            NerfRendererPlugin.update_aabb_crop(new float[3] {aabb_min.x, aabb_min.y, aabb_min.z}, new float[3] {aabb_max.x, aabb_max.y, aabb_max.z});
            has_aabb_updated = true;
        }
        
        if (handle_changed) {
            Debug.Log("Enable Texture");
            rightTexture = Texture2D.CreateExternalTexture(_width, _height, TextureFormat.RGBAFloat, false, true, rightHandle);
            rightMaterial.mainTexture = rightTexture;

            leftTexture = Texture2D.CreateExternalTexture(_width, _height, TextureFormat.RGBAFloat, false, true, leftHandle);
            leftMaterial.mainTexture = leftTexture;

            handle_changed = false;
        }

        if (rightHandle.ToInt32() != 0 && leftHandle.ToInt32() != 0)
        {
            GL.IssuePluginEvent(RenderThreadHandlePtr, DRAW_EVENT);
            GL.InvalidateState();
        }
    }



}
