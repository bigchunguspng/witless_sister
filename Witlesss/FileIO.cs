﻿using Newtonsoft.Json;

namespace Witlesss
{
    public class FileIO<T> : FileIO_Static where T : new()
    {
        private readonly string _path;

        public FileIO(string path) => _path = path;

        public T LoadData()
        {
            if (FileEmptyOrNotExist(_path)) return NewT();

            using var stream = File.OpenText(_path);
            using var reader = new JsonTextReader(stream);
            return Serializer.Deserialize<T>(reader);
        }

        public void SaveData(T db)
        {
            using var stream = File.CreateText(_path);
            using var writer = new JsonTextWriter(stream);
            Serializer.Serialize(writer, db);
        }

        private T NewT()
        {
            CreateFilePath(_path);
            T result = new();
            SaveData(result);
            return result;
        }
    }

    public class FileIO_Static
    {
        protected static readonly JsonSerializer Serializer = new()
        {
            Formatting = Formatting.Indented,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };
    }
}