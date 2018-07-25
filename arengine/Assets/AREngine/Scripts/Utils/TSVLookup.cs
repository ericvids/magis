/************************************************************************************************************

MAGIS copyright © 2018, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEngine;
using System;
using System.Collections.Generic;

public class TSVLookup
{
    private Dictionary<string, object> dictionary = new Dictionary<string, object>();

    public TSVLookup(string path)
    {
        TextAsset asset = Resources.Load<TextAsset>(path);
        string[] rows = asset.text.Replace("\r\n", "\n").Split('\n');
        string[] lastcols = null;
        for (int i = 0; i < rows.Length; i++)
        {
            string[] cols = rows[i].Split('\t');
            if (cols.Length == 1 && cols[0] == "")
                continue;  // ignore blank lines
            if (lastcols != null)
            {
                for (int j = 0; j < cols.Length; j++)
                {
                    string trim = cols[j].Trim().Replace("  ", " ");
                    while (trim != cols[j])
                    {
                        cols[j] = trim;
                        trim = cols[j].Replace("  ", " ");
                    }
                    cols[j] = trim;
                    if (cols[j] == "")
                        cols[j] = lastcols[j];
                }
            }
            Insert(dictionary, cols, 0);
            lastcols = cols;
        }
    }

    private void Insert(Dictionary<string, object> dictionary, string[] values, int fromIndex)
    {
        if (values.Length - fromIndex < 2)
            throw new UnityException("TSV must have at least two columns");
        else if (values.Length - fromIndex == 2)
        {
            if (! dictionary.ContainsKey(values[fromIndex]))
                dictionary.Add(values[fromIndex], (object) new List<string>());
            ((List<string>) dictionary[values[fromIndex]]).Add(values[fromIndex + 1]);
        }
        else
        {
            if (! dictionary.ContainsKey(values[fromIndex]))
                dictionary.Add(values[fromIndex], (object) new Dictionary<string, object>());
            Insert((Dictionary<string, object>) dictionary[values[fromIndex]], values, fromIndex + 1);
        }
    }

    private List<string> Find(Dictionary<string, object> dictionary, string[] values, int fromIndex)
    {
        if (values.Length == 0)
            return new List<string>(dictionary.Keys);
        else if (! dictionary.ContainsKey(values[fromIndex]))
            return new List<string>();
        else
        {
            object obj = dictionary[values[fromIndex]];
            if (obj is List<string>)
            {
                if (fromIndex + 1 == values.Length)
                    return (List<string>) obj;
                else
                    throw new UnityException("Out-of-bounds column " + (fromIndex + 1) + " with key: " + values[fromIndex + 1]);
            }
            else if (fromIndex + 1 == values.Length)
                return new List<string>(((Dictionary<string, object>) obj).Keys);
            else
                return Find((Dictionary<string, object>) obj, values, fromIndex + 1);
        }
    }

    public List<string> Lookup(params string[] keys)
    {
        return Find(dictionary, keys, 0);
    }

    public static void UnitTest()
    {
        TSVLookup tsv = new TSVLookup("UnitTests/TSVLookup");
        Debug.Log(String.Join("\n*", tsv.Lookup("VictoriaStreet", "Examine").ToArray()));
        Debug.Log(String.Join("\n*", tsv.Lookup("VictoriaStreet", "Examine", "Baton").ToArray()));
        Debug.Log(String.Join("\n*", tsv.Lookup("VictoriaStreet", "Examine", "Scissors").ToArray()));
        Debug.Log(String.Join("\n*", tsv.Lookup("VictoriaStreet", "Talk", "Daligmata", "!A").ToArray()));
        try
        {
            Debug.Log(String.Join("\n*", tsv.Lookup("VictoriaStreet", "Talk", "Daligmata", "!A", "something").ToArray()));  // exception
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
        }
        Debug.Log(String.Join("\n*", tsv.Lookup("VictoriaStreet", "Talker", "Daligmata", "!A").ToArray()));  // returns empty list
        Debug.Log(String.Join("\n*", tsv.Lookup("VictoriaStreet", "Talk", "Daligmata", "anotherthing").ToArray()));  // returns empty list
    }
}
