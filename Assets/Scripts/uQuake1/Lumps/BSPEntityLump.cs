using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


public class BSPEntityLump
{
    public string rawEntities;
    public List<Dictionary<string, string>> entityDictionary = new List<Dictionary<string, string>>();

    static public List<Dictionary<string, string>> parseRawEntitiesIntoDictionary(string raw) {
        List<Dictionary<string, string>> entityDictionaryOutput = new List<Dictionary<string, string>>();

        List<List<String>> objects = new List<List<String>>();

        {
            List<String> currentObject = null;
            string currentString = "";
            bool isInsideObject = false;
            bool isInsideString = false;

            foreach (char c in raw)
            {
                if (isInsideObject == false) {
                    if (c == '{') {
                        currentObject = new List<string>();
                        isInsideObject = true;
                    }
                } else {
                    if (isInsideString) {
                        if (c == '\"') {
                            currentObject.Add(currentString);
                            isInsideString = false;
                        } else {
                            currentString += c;
                        }
                    } else {
                        if (c == '}') {
                            objects.Add(currentObject);
                            isInsideObject = false;
                        }

                        if (c == '\"') {
                            currentString = "";
                            isInsideString = true;
                        }
                    }
                }
            }
        }

        foreach (var currentObject in objects) {
            if ((currentObject.Count % 2) == 0) {
                Dictionary<string, string> dictionary = new Dictionary<string, string>();

                string key = "";
                foreach (var str in currentObject)
                {
                    if (key == "") {
                        key = str;
                    } else  {
                        string val = str;
                        dictionary[key] = val;
                        key = "";
                    }
                }

                entityDictionaryOutput.Add(dictionary);
            }
        }

        return entityDictionaryOutput;
    }

    public BSPEntityLump(char[] ents)
    {
        this.rawEntities = new string(ents);
        this.entityDictionary = parseRawEntitiesIntoDictionary(this.rawEntities);
    }
}

