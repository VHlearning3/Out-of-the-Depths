using UnityEditor;
using UnityEngine;

// The Settings page in the Inspector: a button that puts the default rows back, and each row folded to
// "label (type)" showing only the fields its type uses (a heading needs no slider range, a switch no button text).
[CustomEditor(typeof(SettingsPage)), CanEditMultipleObjects]
public class SettingsPageEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        if (GUILayout.Button("Put back the default rows"))
        {
            foreach (Object t in targets)
            {
                Undo.RecordObject(t, "Default settings rows");
                ((SettingsPage)t).FillDefaultRows();
                EditorUtility.SetDirty(t);
            }
            serializedObject.Update();
        }
    }
}

[CustomPropertyDrawer(typeof(SettingsPage.Row))]
public class SettingsRowDrawer : PropertyDrawer
{
    private static readonly string[] TextOnly = { "type", "label" };

    private static string[] FieldsFor(SettingsPage.RowType type)
    {
        switch (type)
        {
            case SettingsPage.RowType.MouseSensitivity:
                return new[] { "type", "label", "min", "max" };
            case SettingsPage.RowType.ResetToDefaults:
                return new[] { "type", "label", "buttonText" };
            case SettingsPage.RowType.Switch:
                return new[] { "type", "label", "defaultOn", "saveKey", "onSwitched" };
            case SettingsPage.RowType.Slider:
                return new[] { "type", "label", "min", "max", "percent", "defaultValue", "saveKey", "onValueChanged" };
            case SettingsPage.RowType.Stepper:
                return new[] { "type", "label", "options", "defaultIndex", "saveKey", "onPicked" };
            case SettingsPage.RowType.Button:
                return new[] { "type", "label", "buttonText", "onPressed" };
            default:
                return TextOnly;
        }
    }

    private static SettingsPage.RowType TypeOf(SerializedProperty row)
    {
        SerializedProperty type = row.FindPropertyRelative("type");
        return type != null ? (SettingsPage.RowType)type.enumValueIndex : SettingsPage.RowType.Heading;
    }

    private static string Title(SerializedProperty row)
    {
        SettingsPage.RowType type = TypeOf(row);
        string kind = ObjectNames.NicifyVariableName(type.ToString());
        SerializedProperty label = row.FindPropertyRelative("label");
        string text = label != null ? label.stringValue.Trim() : "";
        if (text.Length > 42)
            text = text.Substring(0, 40) + "...";
        return text.Length == 0 ? kind : text + "   (" + kind + ")";
    }

    private static GUIContent Name(SettingsPage.RowType type, SerializedProperty field)
    {
        if (field.name == "label" && (type == SettingsPage.RowType.Heading || type == SettingsPage.RowType.Note))
            return new GUIContent("Text", field.tooltip);
        return new GUIContent(field.displayName, field.tooltip);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
            return height;
        foreach (string name in FieldsFor(TypeOf(property)))
        {
            SerializedProperty field = property.FindPropertyRelative(name);
            if (field != null)
                height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(field, true);
        }
        return height + EditorGUIUtility.standardVerticalSpacing;
    }

    public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(rect, label, property);
        var line = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, Title(property), true);
        if (property.isExpanded)
        {
            SettingsPage.RowType type = TypeOf(property);
            EditorGUI.indentLevel++;
            foreach (string name in FieldsFor(type))
            {
                SerializedProperty field = property.FindPropertyRelative(name);
                if (field == null)
                    continue;
                line.y += line.height + EditorGUIUtility.standardVerticalSpacing;
                line.height = EditorGUI.GetPropertyHeight(field, true);
                EditorGUI.PropertyField(line, field, Name(type, field), true);
            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }
}
