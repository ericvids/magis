/************************************************************************************************************

MAGIS copyright © 2015-2019, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEngine;

public class RenderTextureCameraBehaviour : MonoBehaviour
{
    private void FixedUpdate()
    {
        RenderTexture texture = GetComponent<Camera>().targetTexture;
        if (texture.width != Screen.width || texture.height != Screen.height)
        {
            texture.Release();
            if (texture.width != Screen.width)
                texture.width = Screen.width;
            if (texture.height != Screen.height)
                texture.height = Screen.height;
            GetComponent<Camera>().ResetAspect();
        }
    }

    private void OnDestroy()
    {
        RenderTexture texture = GetComponent<Camera>().targetTexture;
        texture.Release();
        texture.width = 1;
        texture.height = 1;
        GetComponent<Camera>().ResetAspect();
    }
}