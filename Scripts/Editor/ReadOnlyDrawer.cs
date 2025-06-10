using UnityEngine;
using UnityEditor;

/// <summary>
/// Unity Editor için ReadOnlyAttribute çizici.
/// Bu, ReadOnlyAttribute kullanılan Inspector alanlarının görünümünü yönetir.
/// </summary>
[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Alanın rengini geçici olarak gri yap
        GUI.enabled = false;
        
        // Özelliği normal şekilde çiz ama etkileşimsiz
        EditorGUI.PropertyField(position, property, label, true);
        
        // Önceki renge geri dön
        GUI.enabled = true;
    }
} 