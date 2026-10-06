// #region license
// Copyright 2026 Utah Departement of Transportation
// for WebUI - theme.ts
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//http://www.apache.org/licenses/LICENSE-2.
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// #endregion
import { createTheme } from '@mui/material/styles'
import { createContext, useEffect, useMemo, useState } from 'react'

type modeOptions = 'light' | 'dark'

const COLOR_MODE_STORAGE_KEY = 'colorMode'

export const themeSettings = (mode: modeOptions) => {
  return {
    palette: {
      mode: mode,
      ...(mode === 'dark'
        ? {
            primary: {
              light: '#6b93cc',
              main: '#5a87c6',
              dark: '#517ab2',
            },
            secondary: {
              light: '#0a203d',
              main: '#0b2444',
              dark: '#233a57',
            },
            accent: {
              light: '#777efb',
              main: '#6870fa',
              dark: '#5e65e1',
            },
            orange: {
              main: '#e86924',
              light: '#ea783a',
              dark: '#d15f20',
            },
            background: {
              default: '#141b2d',
              paper: '#1f2a40',
              highlight: '#1F2A40',
            },
          }
        : {
            primary: {
              light: '#0c6ecc',
              main: '#09549c',
              dark: '#063a6c',
            },
            secondary: {
              light: '#0a203d',
              main: '#0b2444',
              dark: '#233a57',
            },
            accent: {
              light: '#777efb',
              main: '#6870fa',
              dark: '#5e65e1',
            },
            orange: {
              main: '#e86924',
              light: '#ea783a',
              dark: '#d15f20',
            },
            background: {
              default: '#f9f9fb',
              paper: '#fff',
              highlight: '#eeeeee',
            },
          }),
    },
    typography: {
      // fontFamily: ['Source Sans Pro', 'sans-serif'].join(','),
      // fontSize: '1rem',
      h1: {
        fontFamily: ['Source Sans Pro', 'sans-serif'].join(','),
        fontSize: 40,
      },
      h2: {
        fontFamily: ['Source Sans Pro', 'sans-serif'].join(','),
        fontSize: 32,
      },
      h3: {
        fontFamily: ['Source Sans Pro', 'sans-serif'].join(','),
        fontSize: 24,
      },
      h4: {
        fontFamily: ['Source Sans Pro', 'sans-serif'].join(','),
        fontSize: 20,
      },
      h5: {
        fontFamily: ['Source Sans Pro', 'sans-serif'].join(','),
        fontSize: 16,
      },
      h6: {
        fontFamily: ['Source Sans Pro', 'sans-serif'].join(','),
        fontSize: 14,
      },
    },
  }
}

export const ColorModeContext = createContext({
  toggleColorMode: () => {
    console.warn('toggleColorMode is not implemented.')
  },
  setColorMode: (_mode: modeOptions) => {
    console.warn('setColorMode is not implemented.')
  },
})

const saveColorMode = (mode: modeOptions) => {
  try {
    localStorage.setItem(COLOR_MODE_STORAGE_KEY, mode)
  } catch {
    // storage unavailable, mode just won't persist
  }
}

export const useMode = () => {
  const [mode, setMode] = useState<modeOptions>('light')

  // Read the saved mode after mount (localStorage isn't available during SSR)
  useEffect(() => {
    try {
      const saved = localStorage.getItem(COLOR_MODE_STORAGE_KEY)
      if (saved === 'light' || saved === 'dark') setMode(saved)
    } catch {
      // storage unavailable, keep default
    }
  }, [])

  const colorMode = useMemo(
    () => ({
      toggleColorMode: () => {
        setMode((prev) => {
          const next = prev === 'light' ? 'dark' : 'light'
          saveColorMode(next)
          return next
        })
      },
      setColorMode: (next: modeOptions) => {
        saveColorMode(next)
        setMode(next)
      },
    }),
    []
  )

  const theme = useMemo(() => createTheme(themeSettings(mode)), [mode])
  return [theme, colorMode] as const
}
