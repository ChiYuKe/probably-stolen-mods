using UnityEngine;

namespace Chemistry
{
    internal sealed class LabEntranceButton : MonoBehaviour
    {
        private CustomUIElement entry;
        private Canvas canvas;

        private void LateUpdate()
        {
            RenderHandler renderer = RenderHandler.current;
            Canvas currentCanvas = renderer != null ? renderer.windowUICanvas : null;
            if (canvas != currentCanvas)
            {
                RemoveButton();
                canvas = currentCanvas;
            }

            bool show = canvas != null && IsAtStore();
            if (show && entry?.IsAlive != true)
            {
                CreateButton();
            }

            if (entry?.IsAlive != true)
            {
                return;
            }

            entry.SetVisible(show);
            if (!show)
            {
                return;
            }

            entry.SetInteractable(CanEnter());
            Position();
        }

        private void CreateButton()
        {
            CustomUIManager manager = CustomUIManager.Instance;
            if (manager == null)
            {
                return;
            }

            GameObject root = manager.SpawnElement(CustomUIManager.ElementKind.Button,
                canvas.transform, null, "ChemistryLabEntrance");
            if (root == null)
            {
                return;
            }

            entry = CustomUIElement.Bind(root, "chemistry_lab_entrance");
            if (entry.Rect == null || entry.button == null)
            {
                Destroy(root);
                entry = null;
                return;
            }

            RectTransform rect = entry.Rect;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(160f, 34f);
            entry.SetText(Loc.Get("lab_enter_from_store", "前往地下室", "Go to basement"));
            entry.OnClick(() =>
            {
                if (IsAtStore() && CanEnter())
                {
                    LabLocation.TravelHere();
                }
            });
        }

        private void Position()
        {
            var parent = (RectTransform)canvas.transform;
            RectTransform rect = entry.Rect;
            LetterboxManager.ComputeGameArea(Screen.width, Screen.height, out int x, out int y,
                out _, out _);
            float scale = canvas.scaleFactor;
            var screenPoint = new Vector2(x + 16f * scale, y + 84f * scale);
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, camera, out Vector2 point))
            {
                rect.anchoredPosition = point - parent.rect.min;
            }
        }

        private static bool IsAtStore()
        {
            MapUIManager map = MapUIManager.Instance;
            StoreUIManager store = StoreUIManager.Instance;
            return LabLocation.Registered != null && PlayerStore.HasInstance && map != null &&
                store != null && store.isActiveAndEnabled && !map.IsOutside && !map.IsInTransit &&
                !TransitUI.IsOpen && (map.confirmationPanel == null || !map.confirmationPanel.activeInHierarchy) &&
                (store.blackscreen == null || !store.blackscreen.gameObject.activeInHierarchy);
        }

        private static bool CanEnter()
        {
            PlayerStore player = PlayerStore.Instance;
            return player != null && (player.devMode ||
                (!HealthData.IsSeriouslyWounded() && !StartingPerkContent.IsAgoraphobiaResting() &&
                StoreStation.Instance.storeEventManager?.IsEventActive("crackdown") == false));
        }

        private void OnDestroy()
        {
            RemoveButton();
        }

        private void RemoveButton()
        {
            if (entry?.IsAlive == true)
            {
                entry.SetVisible(false);
                Destroy(entry.root);
            }

            entry = null;
        }
    }
}
