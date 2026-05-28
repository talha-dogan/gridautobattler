using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class AssignSounds
{
    public static void Execute()
    {
        // SoundManager objesini bul
        SoundManager sm = GameObject.Find("--- MANAGERS ---/GlobalManagers/SoundManager")
                          ?.GetComponent<SoundManager>();

        if (sm == null)
        {
            // Alternatif arama
            sm = Object.FindAnyObjectByType<SoundManager>();
        }

        if (sm == null)
        {
            Debug.LogError("[AssignSounds] SoundManager bulunamadı!");
            return;
        }

        // Ses dosyalarını yükle
        AudioClip shoot    = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Saund/SFX/pistol-shot.mp3");
        AudioClip hit      = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Saund/SFX/hit.mp3");
        AudioClip death    = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Saund/SFX/death.mp3");
        AudioClip win      = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Saund/SFX/win.mp3");
        AudioClip woosh    = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Saund/SFX/woosh.mp3");
        AudioClip battleBG = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Saund/BGMusic/Play.mp3");
        AudioClip menuBG   = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Saund/BGMusic/Menu.mp3");

        // Yükleme kontrolü
        Debug.Log($"[AssignSounds] shoot={shoot?.name}, hit={hit?.name}, death={death?.name}, win={win?.name}, woosh={woosh?.name}, battleBG={battleBG?.name}, menuBG={menuBG?.name}");

        // SerializedObject ile Inspector alanlarını doldur
        SerializedObject so = new SerializedObject(sm);

        // BGM alanları
        so.FindProperty("battleBGM").objectReferenceValue = battleBG;
        so.FindProperty("menuBGM").objectReferenceValue   = menuBG;

        // soundLibrary dizisini oluştur
        SerializedProperty library = so.FindProperty("soundLibrary");
        library.arraySize = 6;

        // ── 0: WeaponShoot ──────────────────────────────────────────────────
        SetEntry(library.GetArrayElementAtIndex(0),
            soundType: 0, // WeaponShoot
            clips: new AudioClip[] { shoot },
            volume: 0.8f, pitchMin: 0.9f, pitchMax: 1.1f);

        // ── 1: WeaponMeleeSwing ─────────────────────────────────────────────
        SetEntry(library.GetArrayElementAtIndex(1),
            soundType: 1, // WeaponMeleeSwing
            clips: new AudioClip[] { woosh },
            volume: 0.7f, pitchMin: 0.9f, pitchMax: 1.1f);

        // ── 2: HitFlesh ─────────────────────────────────────────────────────
        SetEntry(library.GetArrayElementAtIndex(2),
            soundType: 2, // HitFlesh
            clips: new AudioClip[] { hit },
            volume: 0.6f, pitchMin: 0.85f, pitchMax: 1.15f);

        // ── 3: UnitDeath ────────────────────────────────────────────────────
        SetEntry(library.GetArrayElementAtIndex(3),
            soundType: 4, // UnitDeath
            clips: new AudioClip[] { death },
            volume: 0.75f, pitchMin: 0.9f, pitchMax: 1.1f);

        // ── 4: BattleWin ────────────────────────────────────────────────────
        SetEntry(library.GetArrayElementAtIndex(4),
            soundType: 5, // BattleWin
            clips: new AudioClip[] { win },
            volume: 1.0f, pitchMin: 1.0f, pitchMax: 1.0f);

        // ── 5: BattleLose ───────────────────────────────────────────────────
        SetEntry(library.GetArrayElementAtIndex(5),
            soundType: 6, // BattleLose
            clips: new AudioClip[] { death },
            volume: 0.9f, pitchMin: 0.8f, pitchMax: 0.9f);

        so.ApplyModifiedProperties();

        // Sahneyi kaydet
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[AssignSounds] Tüm ses dosyaları SoundManager'a başarıyla atandı!");
    }

    private static void SetEntry(SerializedProperty entry, int soundType,
        AudioClip[] clips, float volume, float pitchMin, float pitchMax)
    {
        entry.FindPropertyRelative("soundType").enumValueIndex = soundType;
        entry.FindPropertyRelative("volume").floatValue        = volume;
        entry.FindPropertyRelative("pitchMin").floatValue      = pitchMin;
        entry.FindPropertyRelative("pitchMax").floatValue      = pitchMax;

        SerializedProperty clipsArr = entry.FindPropertyRelative("clips");
        clipsArr.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
            clipsArr.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
    }
}
