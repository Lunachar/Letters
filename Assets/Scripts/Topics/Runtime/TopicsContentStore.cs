using System;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class TopicsContentStore
{
    private const string SaveFileName = "topics-save.json";

    private readonly TopicsGameConfig config;
    private readonly string savePath;
    private TopicsSaveData saveData;

    public TopicsContentStore(TopicsGameConfig config)
    {
        this.config = config;
        savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
        Load();
    }

    public List<TopicRoomRuntime> GetRooms()
    {
        List<TopicRoomRuntime> rooms = new List<TopicRoomRuntime>();

        if (config != null && config.startingRooms != null)
        {
            foreach (TopicRoomData room in config.startingRooms)
            {
                if (room != null)
                {
                    rooms.Add(FromAsset(room));
                }
            }
        }

        foreach (TopicRoomRecord record in saveData.rooms)
        {
            rooms.Add(FromRecord(record));
        }

        return rooms;
    }

    public TopicScoreRecord GetScore(string roomId)
    {
        return saveData.scores.Find(score => score.roomId == roomId);
    }

    public void RegisterScore(string roomId, int correct, int total)
    {
        TopicScoreRecord score = saveData.scores.Find(item => item.roomId == roomId);
        if (score == null)
        {
            score = new TopicScoreRecord { roomId = roomId };
            saveData.scores.Add(score);
        }

        if (correct > score.bestCorrect || score.bestTotal == 0 || correct == score.bestCorrect && total < score.bestTotal)
        {
            score.bestCorrect = correct;
            score.bestTotal = total;
            score.bestDateUtc = DateTime.UtcNow.ToString("O");
            Save();
        }
    }

    public void UpsertUserRoom(TopicRoomRuntime room)
    {
        TopicRoomRecord record = ToRecord(room);
        int index = saveData.rooms.FindIndex(item => item.id == room.id);
        if (index >= 0)
        {
            saveData.rooms[index] = record;
        }
        else
        {
            saveData.rooms.Add(record);
        }

        Save();
    }

    public void DeleteUserRoom(string roomId)
    {
        saveData.rooms.RemoveAll(room => room.id == roomId);
        saveData.scores.RemoveAll(score => score.roomId == roomId);
        Save();
    }

    public TopicRoomRuntime CreateEmptyRoom()
    {
        TopicRoomRuntime room = new TopicRoomRuntime
        {
            id = Guid.NewGuid().ToString("N"),
            title = "Новая тема",
            cardSymbol = "+",
            cardColor = new Color(0.42f, 0.53f, 0.75f),
            useTextToSpeech = true,
            speechVolume = config != null ? config.speechVolume : 1f,
            speechRate = config != null ? config.speechRate : 0.95f,
            speechPitch = config != null ? config.speechPitch : 1f,
            androidLanguage = config != null ? config.androidLanguage : "ru_RU",
            introEnabled = true,
            defaultAnswersToShow = 4,
            rewardEffectEnabled = true,
            rewardMessage = "Отличная работа!",
            isUserCreated = true
        };

        room.introPages.Add(new TopicIntroPageRuntime { text = "Короткое объяснение темы." });
        room.questions.Add(CreateSampleQuestion());
        return room;
    }

    public TopicQuestionRuntime CreateSampleQuestion()
    {
        TopicQuestionRuntime question = new TopicQuestionRuntime
        {
            id = Guid.NewGuid().ToString("N"),
            text = "Выбери правильный ответ",
            answersToShow = 4
        };
        question.answers.Add(new TopicAnswerRuntime { id = Guid.NewGuid().ToString("N"), text = "Верно", textSize = 52, textColor = Color.white, isCorrect = true });
        question.answers.Add(new TopicAnswerRuntime { id = Guid.NewGuid().ToString("N"), text = "Ответ 2", textSize = 52, textColor = Color.white });
        question.answers.Add(new TopicAnswerRuntime { id = Guid.NewGuid().ToString("N"), text = "Ответ 3", textSize = 52, textColor = Color.white });
        question.answers.Add(new TopicAnswerRuntime { id = Guid.NewGuid().ToString("N"), text = "Ответ 4", textSize = 52, textColor = Color.white });
        return question;
    }

    public static Sprite LoadSprite(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        byte[] bytes = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes))
        {
            UnityEngine.Object.Destroy(texture);
            return null;
        }

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    public static IEnumerator LoadAudioClip(string path, Action<AudioClip> onLoaded)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            onLoaded?.Invoke(null);
            yield break;
        }

        AudioType audioType = GetAudioType(path);
        string uri = new Uri(path).AbsoluteUri;
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(uri, audioType))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("TopicsContentStore: cannot load audio: " + request.error);
                onLoaded?.Invoke(null);
                yield break;
            }

            onLoaded?.Invoke(DownloadHandlerAudioClip.GetContent(request));
        }
    }

    private void Load()
    {
        if (!File.Exists(savePath))
        {
            saveData = new TopicsSaveData();
            return;
        }

        try
        {
            saveData = JsonUtility.FromJson<TopicsSaveData>(File.ReadAllText(savePath)) ?? new TopicsSaveData();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("TopicsContentStore: cannot load save file: " + exception.Message);
            saveData = new TopicsSaveData();
        }
    }

    private static AudioType GetAudioType(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        switch (extension)
        {
            case ".mp3":
                return AudioType.MPEG;
            case ".ogg":
                return AudioType.OGGVORBIS;
            case ".wav":
                return AudioType.WAV;
            default:
                return AudioType.UNKNOWN;
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(savePath));
        File.WriteAllText(savePath, JsonUtility.ToJson(saveData, true));
    }

    private TopicRoomRuntime FromAsset(TopicRoomData asset)
    {
        TopicRoomRuntime room = new TopicRoomRuntime
        {
            id = string.IsNullOrEmpty(asset.id) ? asset.name : asset.id,
            title = asset.title,
            cardSymbol = asset.cardSymbol,
            icon = asset.icon,
            cardColor = asset.cardColor,
            roomMusic = asset.roomMusic,
            rewardMusic = asset.rewardMusic,
            useTextToSpeech = asset.useTextToSpeech,
            speechVolume = asset.speechVolume,
            speechRate = asset.speechRate,
            speechPitch = asset.speechPitch,
            androidLanguage = asset.androidLanguage,
            introEnabled = asset.introEnabled,
            autoStartAfterIntro = asset.autoStartAfterIntro,
            defaultAnswersToShow = asset.defaultAnswersToShow,
            questionsPerRun = asset.questionsPerRun,
            rewardEffectEnabled = asset.rewardEffectEnabled,
            rewardMessage = asset.rewardMessage,
            isUserCreated = false
        };

        if (asset.introPages != null)
        {
            foreach (TopicIntroPageData intro in asset.introPages)
            {
                TopicIntroPageRuntime page = new TopicIntroPageRuntime
                {
                    text = intro.text,
                    narration = intro.narration
                };

                if (intro.photos != null)
                {
                    page.photos.AddRange(intro.photos);
                }

                room.introPages.Add(page);
            }
        }

        if (asset.questions != null)
        {
            foreach (TopicQuestionData question in asset.questions)
            {
                TopicQuestionRuntime runtimeQuestion = new TopicQuestionRuntime
                {
                    id = string.IsNullOrEmpty(question.id) ? Guid.NewGuid().ToString("N") : question.id,
                    text = question.text,
                    image = question.image,
                    questionSound = question.questionSound,
                    answersToShow = question.answersToShow
                };

                if (question.answers != null)
                {
                    foreach (TopicAnswerData answer in question.answers)
                    {
                        runtimeQuestion.answers.Add(new TopicAnswerRuntime
                        {
                            id = string.IsNullOrEmpty(answer.id) ? Guid.NewGuid().ToString("N") : answer.id,
                            text = answer.text,
                            textSize = answer.textSize,
                            textColor = answer.textColor,
                            image = answer.image,
                            sound = answer.sound,
                            isCorrect = answer.isCorrect
                        });
                    }
                }

                room.questions.Add(runtimeQuestion);
            }
        }

        return room;
    }

    private TopicRoomRuntime FromRecord(TopicRoomRecord record)
    {
        Color cardColor = new Color(0.42f, 0.53f, 0.75f);
        ColorUtility.TryParseHtmlString(record.cardColorHtml, out cardColor);

        TopicRoomRuntime room = new TopicRoomRuntime
        {
            id = record.id,
            title = record.title,
            cardSymbol = record.cardSymbol,
            iconPath = record.iconPath,
            icon = LoadSprite(record.iconPath),
            cardColor = cardColor,
            musicPath = record.musicPath,
            rewardMusicPath = record.rewardMusicPath,
            useTextToSpeech = record.useTextToSpeech,
            speechVolume = record.speechVolume,
            speechRate = record.speechRate,
            speechPitch = record.speechPitch,
            androidLanguage = record.androidLanguage,
            introEnabled = record.introEnabled,
            autoStartAfterIntro = record.autoStartAfterIntro,
            defaultAnswersToShow = record.defaultAnswersToShow,
            questionsPerRun = record.questionsPerRun,
            rewardEffectEnabled = record.rewardEffectEnabled,
            rewardMessage = string.IsNullOrEmpty(record.rewardMessage) ? "Отличная работа!" : record.rewardMessage,
            isUserCreated = true
        };

        foreach (TopicIntroPageRecord intro in record.introPages)
        {
            TopicIntroPageRuntime page = new TopicIntroPageRuntime
            {
                text = intro.text,
                narrationPath = intro.narrationPath
            };

            foreach (string photoPath in intro.photoPaths)
            {
                page.photoPaths.Add(photoPath);
                Sprite sprite = LoadSprite(photoPath);
                if (sprite != null)
                {
                    page.photos.Add(sprite);
                }
            }

            room.introPages.Add(page);
        }

        foreach (TopicQuestionRecord question in record.questions)
        {
            TopicQuestionRuntime runtimeQuestion = new TopicQuestionRuntime
            {
                id = question.id,
                text = question.text,
                imagePath = question.imagePath,
                image = LoadSprite(question.imagePath),
                questionSoundPath = question.questionSoundPath,
                answersToShow = question.answersToShow
            };

            foreach (TopicAnswerRecord answer in question.answers)
            {
                Color textColor = Color.white;
                ColorUtility.TryParseHtmlString(answer.textColorHtml, out textColor);
                runtimeQuestion.answers.Add(new TopicAnswerRuntime
                {
                    id = answer.id,
                    text = answer.text,
                    textSize = answer.textSize,
                    textColor = textColor,
                    imagePath = answer.imagePath,
                    image = LoadSprite(answer.imagePath),
                    soundPath = answer.soundPath,
                    isCorrect = answer.isCorrect
                });
            }

            room.questions.Add(runtimeQuestion);
        }

        return room;
    }

    private TopicRoomRecord ToRecord(TopicRoomRuntime room)
    {
        TopicRoomRecord record = new TopicRoomRecord
        {
            id = room.id,
            title = room.title,
            cardSymbol = room.cardSymbol,
            cardColorHtml = "#" + ColorUtility.ToHtmlStringRGBA(room.cardColor),
            iconPath = room.iconPath,
            musicPath = room.musicPath,
            rewardMusicPath = room.rewardMusicPath,
            useTextToSpeech = room.useTextToSpeech,
            speechVolume = room.speechVolume,
            speechRate = room.speechRate,
            speechPitch = room.speechPitch,
            androidLanguage = room.androidLanguage,
            introEnabled = room.introEnabled,
            autoStartAfterIntro = room.autoStartAfterIntro,
            defaultAnswersToShow = room.defaultAnswersToShow,
            questionsPerRun = room.questionsPerRun,
            rewardEffectEnabled = room.rewardEffectEnabled,
            rewardMessage = room.rewardMessage
        };

        foreach (TopicIntroPageRuntime intro in room.introPages)
        {
            TopicIntroPageRecord page = new TopicIntroPageRecord
            {
                text = intro.text,
                narrationPath = intro.narrationPath
            };
            page.photoPaths.AddRange(intro.photoPaths);
            record.introPages.Add(page);
        }

        foreach (TopicQuestionRuntime question in room.questions)
        {
            TopicQuestionRecord questionRecord = new TopicQuestionRecord
            {
                id = question.id,
                text = question.text,
                imagePath = question.imagePath,
                questionSoundPath = question.questionSoundPath,
                answersToShow = question.answersToShow
            };

            foreach (TopicAnswerRuntime answer in question.answers)
            {
                questionRecord.answers.Add(new TopicAnswerRecord
                {
                    id = answer.id,
                    text = answer.text,
                    textSize = answer.textSize,
                    textColorHtml = "#" + ColorUtility.ToHtmlStringRGBA(answer.textColor),
                    imagePath = answer.imagePath,
                    soundPath = answer.soundPath,
                    isCorrect = answer.isCorrect
                });
            }

            record.questions.Add(questionRecord);
        }

        return record;
    }
}
