using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class CirclePacker : EditorWindow
    {
        private bool alignHeight = true; // Выравнивать высоту
        private float padding = 0.1f; // Отступ между объектами
        private bool randomRotation = true; // Случайный поворот по Y
        private bool sortBySize = true; // Сортировать: крупные в центр, мелкие наружу

        private void OnGUI()
        {
            GUILayout.Label("Packing Settings", EditorStyles.boldLabel);

            padding = EditorGUILayout.FloatField("Padding", padding);
            sortBySize = EditorGUILayout.Toggle("Big to Center", sortBySize);
            randomRotation = EditorGUILayout.Toggle("Random Y Rotation", randomRotation);
            alignHeight = EditorGUILayout.Toggle("Align Y Position", alignHeight);

            GUILayout.Space(10);

            if (GUILayout.Button("Pack Objects")) PackObjects();
        }

        [MenuItem("Tools/Circle Packer (Inside)")]
        public static void ShowWindow()
        {
            GetWindow<CirclePacker>("Circle Packer");
        }

        private void PackObjects()
        {
            var selection = Selection.gameObjects;

            if (selection.Length == 0)
            {
                Debug.LogWarning("No objects selected.");
                return;
            }

            // 1. Отсеиваем детей, чтобы двигать только корни
            var roots = selection.Where(g => !IsChildOfAny(g.transform, selection.Select(x => x.transform))).ToList();

            if (roots.Count == 0) return;

            Undo.RecordObjects(roots.Select(x => x.transform).ToArray(), "Pack Objects");

            // 2. Подготавливаем данные: вычисляем радиусы всех объектов
            var items = new List<PackItem>();
            var center = Vector3.zero;

            foreach (var obj in roots)
            {
                center += obj.transform.position;
                // Вычисляем радиус (половина ширины)
                var radius = GetObjectRadius(obj);
                items.Add(new PackItem { Transform = obj.transform, Radius = radius });
            }

            center /= roots.Count;

            // 3. Сортировка (опционально)
            if (sortBySize)
                // Сортируем от большего к меньшему
                items.Sort((a, b) => b.Radius.CompareTo(a.Radius));

            // 4. Алгоритм размещения
            // Список уже размещенных объектов для проверки коллизий
            var placedItems = new List<PackItem>();

            foreach (var item in items)
            {
                // Ищем позицию по спирали
                var finalPos = FindFreePosition(center, item.Radius, placedItems);

                // Применяем позицию
                if (alignHeight)
                    item.Transform.position = new Vector3(finalPos.x, center.y, finalPos.z);
                else
                    item.Transform.position = new Vector3(finalPos.x, item.Transform.position.y, finalPos.z);

                // Случайный поворот
                if (randomRotation)
                {
                    var rot = item.Transform.rotation.eulerAngles;
                    item.Transform.rotation = Quaternion.Euler(rot.x, Random.Range(0f, 360f), rot.z);
                }

                // Обновляем позицию в структуре (для следующих проверок)
                item.Position = item.Transform.position;
                placedItems.Add(item);
            }

            Debug.Log($"Packed {roots.Count} objects.");
        }

        // "Щупаем" пространство по спирали, пока не найдем свободное место
        private Vector3 FindFreePosition(Vector3 center, float currentRadius, List<PackItem> placedItems)
        {
            if (placedItems.Count == 0) return center;

            // Параметры спирали
            var angle = 0f;
            var distance = 0f;
            var angleStep = 0.5f; // Шаг угла в радианах (чем меньше, тем точнее, но медленнее)
            // Шаг дистанции зависит от размера объекта, чтобы не пропускать дырки, 
            // но для скорости берем константу или долю радиуса
            var distStep = 0.05f;

            var safetyCounter = 10000; // Защита от зависания

            while (safetyCounter > 0)
            {
                var x = Mathf.Cos(angle) * distance;
                var z = Mathf.Sin(angle) * distance;
                var candidatePos = center + new Vector3(x, 0, z);

                if (!CheckCollision(candidatePos, currentRadius, placedItems)) return candidatePos;

                angle += angleStep;
                // Увеличиваем радиус спирали. 
                // Чтобы спираль была плотной, увеличиваем distance медленно.
                distance += distStep;

                safetyCounter--;
            }

            Debug.LogWarning("Could not find free spot via spiral, placing at edge.");
            return center + new Vector3(distance, 0, 0);
        }

        private bool CheckCollision(Vector3 candidatePos, float currentRadius, List<PackItem> placedItems)
        {
            foreach (var placed in placedItems)
            {
                var dist = Vector3.Distance(
                    new Vector3(candidatePos.x, 0, candidatePos.z),
                    new Vector3(placed.Position.x, 0, placed.Position.z)
                );

                // Проверка пересечения двух кругов: Dist < R1 + R2 + Padding
                if (dist < currentRadius + placed.Radius + padding) return true; // Пересекается
            }

            return false;
        }

        private float GetObjectRadius(GameObject obj)
        {
            var colliders = obj.GetComponentsInChildren<Collider>();
            if (colliders.Length == 0) return Mathf.Max(obj.transform.lossyScale.x, obj.transform.lossyScale.z) * 0.5f;

            var combinedBounds = colliders[0].bounds;
            for (var i = 1; i < colliders.Length; i++) combinedBounds.Encapsulate(colliders[i].bounds);

            // Радиус = половина диагонали bounding box'а на плоскости XZ, 
            // или просто макс сторона пополам. Для круглой упаковки лучше брать макс экстент.
            return Mathf.Max(combinedBounds.extents.x, combinedBounds.extents.z);
        }

        private bool IsChildOfAny(Transform target, IEnumerable<Transform> potentialParents)
        {
            foreach (var parent in potentialParents)
                if (target != parent && target.IsChildOf(parent))
                    return true;

            return false;
        }

        // Вспомогательный класс для хранения данных в процессе
        private class PackItem
        {
            public Vector3 Position; // Кэшируем позицию для скорости
            public float Radius;
            public Transform Transform;
        }
    }
}