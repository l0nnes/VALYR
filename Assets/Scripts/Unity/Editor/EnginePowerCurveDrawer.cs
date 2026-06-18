using Core.Math;
using UnityEditor;
using UnityEngine;

namespace Unity.Editor
{
    [CustomPropertyDrawer(typeof(EnginePowerCurve))]
    public class EnginePowerCurveDrawer : PropertyDrawer
    {
        private const float GraphHeight = 150f;
        private const int Resolution = 72;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var labelRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, label, EditorStyles.boldLabel);

            var graphRect = new Rect(position.x, labelRect.yMax + 5f, position.width, GraphHeight);
            DrawGraph(graphRect, property);

            var fieldRect = new Rect(position.x, graphRect.yMax + 6f, position.width,
                EditorGUIUtility.singleLineHeight);
            DrawProp(ref fieldRect, property.FindPropertyRelative("idleRpm"));
            DrawProp(ref fieldRect, property.FindPropertyRelative("maxRpm"));

            var points = property.FindPropertyRelative("points");
            EditorGUI.PropertyField(fieldRect, points, true);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var points = property.FindPropertyRelative("points");
            return EditorGUIUtility.singleLineHeight * 3f
                   + GraphHeight
                   + 18f
                   + EditorGUI.GetPropertyHeight(points, true);
        }

        private static void DrawGraph(Rect rect, SerializedProperty property)
        {
            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1f));

            var curve = BuildCurve(property);
            var maxRpm = Mathf.Max(curve.maxRpm, curve.idleRpm + 1f);
            var maxPower = 1f;
            var maxTorque = 1f;

            for (var i = 0; i < Resolution; i++)
            {
                var rpm = Mathf.Lerp(0f, maxRpm, i / (float)(Resolution - 1));
                maxPower = Mathf.Max(maxPower, curve.EvaluateHorsePower(rpm));
                maxTorque = Mathf.Max(maxTorque, curve.EvaluateTorqueNm(rpm));
            }

            var powerPoints = new Vector3[Resolution];
            var torquePoints = new Vector3[Resolution];

            for (var i = 0; i < Resolution; i++)
            {
                var rpm = Mathf.Lerp(0f, maxRpm, i / (float)(Resolution - 1));
                var x = rect.x + rpm / maxRpm * rect.width;

                powerPoints[i] = new Vector3(x, rect.yMax - curve.EvaluateHorsePower(rpm) / maxPower * rect.height, 0f);
                torquePoints[i] = new Vector3(x, rect.yMax - curve.EvaluateTorqueNm(rpm) / maxTorque * rect.height, 0f);
            }

            Handles.color = new Color(1f, 1f, 1f, 0.06f);
            for (var i = 1; i < 4; i++)
            {
                var y = rect.yMax - rect.height * 0.25f * i;
                Handles.DrawLine(new Vector3(rect.x, y, 0f), new Vector3(rect.xMax, y, 0f));
            }

            Handles.color = Color.green;
            Handles.DrawAAPolyLine(3f, torquePoints);
            Handles.color = Color.red;
            Handles.DrawAAPolyLine(3f, powerPoints);

            Handles.color = new Color(0.35f, 1f, 0.35f, 1f);
            for (var i = 0; i < curve.points.Length; i++)
            {
                var rpm = Mathf.Clamp(curve.points[i].rpm, 0f, maxRpm);
                var torque = Mathf.Max(0f, curve.points[i].torqueNm);
                var x = rect.x + rpm / maxRpm * rect.width;
                var y = rect.yMax - torque / maxTorque * rect.height;
                Handles.DrawSolidDisc(new Vector3(x, y, 0f), Vector3.forward, 3.5f);
            }

            GUI.color = Color.green;
            GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, 120f, 18f), "Torque (Nm)", EditorStyles.miniLabel);
            GUI.color = Color.red;
            GUI.Label(new Rect(rect.x + 6f, rect.y + 20f, 120f, 18f), "Power (HP)", EditorStyles.miniLabel);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.xMax - 105f, rect.y + 4f, 100f, 18f), $"{maxTorque:F0} Nm", EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.xMax - 105f, rect.y + 20f, 100f, 18f), $"{maxPower:F0} hp", EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.x + 6f, rect.yMax - 18f, 100f, 18f), "0 rpm", EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.xMax - 95f, rect.yMax - 18f, 90f, 18f), $"{maxRpm:F0} rpm", EditorStyles.miniLabel);
        }

        private static EnginePowerCurve BuildCurve(SerializedProperty property)
        {
            var serializedPoints = property.FindPropertyRelative("points");
            var curve = new EnginePowerCurve
            {
                idleRpm = property.FindPropertyRelative("idleRpm").floatValue,
                maxRpm = property.FindPropertyRelative("maxRpm").floatValue,
                points = new EnginePowerCurve.TorquePoint[serializedPoints.arraySize]
            };

            for (var i = 0; i < serializedPoints.arraySize; i++)
            {
                var point = serializedPoints.GetArrayElementAtIndex(i);
                curve.points[i] = new EnginePowerCurve.TorquePoint
                {
                    rpm = point.FindPropertyRelative("rpm").floatValue,
                    torqueNm = point.FindPropertyRelative("torqueNm").floatValue
                };
            }

            return curve;
        }

        private static void DrawProp(ref Rect rect, SerializedProperty prop)
        {
            EditorGUI.PropertyField(rect, prop);
            rect.y += EditorGUIUtility.singleLineHeight + 2f;
        }
    }
}