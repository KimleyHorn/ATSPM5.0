import { useSidebarStore } from '@/stores/sidebar'
import ChevronRightIcon from '@mui/icons-material/ChevronRight'
import {
  Box,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  MenuItem,
  Paper,
  Popper,
  Typography,
  useTheme,
} from '@mui/material'
import { useRouter } from 'next/router'
import React from 'react'

type SubItem = {
  text: string
  url?: string
  onClick?: () => void
  selected?: boolean
}

type NavItemFlyoutProps = {
  icon?: React.ReactNode
  text: string
  subItems: SubItem[]
}

export default function NavItemFlyout({
  icon,
  text,
  subItems,
}: NavItemFlyoutProps) {
  const router = useRouter()
  const theme = useTheme()
  const { toggleSidebar } = useSidebarStore()

  const [open, setOpen] = React.useState(false)
  const anchorRef = React.useRef<HTMLDivElement | null>(null)
  const popperRef = React.useRef<HTMLDivElement | null>(null)

  const handleParentMouseEnter = () => {
    setOpen(true)
  }

  // relatedTarget isn't always a DOM node (e.g. leaving the window), and contains() throws on non-nodes
  const isInside = (container: HTMLElement | null, target: EventTarget | null) =>
    target instanceof Node && !!container?.contains(target)

  const handleParentMouseLeave = (e: React.MouseEvent) => {
    if (isInside(popperRef.current, e.relatedTarget)) {
      return
    }
    setOpen(false)
  }

  const handlePopperMouseEnter = () => {
    setOpen(true)
  }

  const handlePopperMouseLeave = (e: React.MouseEvent) => {
    if (isInside(anchorRef.current, e.relatedTarget)) {
      return
    }
    setOpen(false)
  }

  const handleMenuItemClick = (item: SubItem) => {
    if (item.onClick) {
      item.onClick()
    } else if (item.url) {
      toggleSidebar()
      router.push(item.url)
    }
    setOpen(false)
  }

  const isChildActive = subItems.some(
    (item) => item.url !== undefined && router.asPath === item.url
  )
  const baseColor = theme.palette.primary.main

  return (
    <>
      <Box
        ref={anchorRef}
        onMouseEnter={handleParentMouseEnter}
        onMouseLeave={handleParentMouseLeave}
      >
        <ListItemButton
          selected={isChildActive}
          sx={{
            py: 0.5,
            color: theme.palette.text.primary,
            '&.Mui-selected': {
              backgroundColor: `${baseColor}30`,
              borderRight: `4px solid ${baseColor}`,
            },
            '&:hover': {
              backgroundColor: `${baseColor}30`,
            },
          }}
        >
          {icon && (
            <ListItemIcon sx={{ minWidth: '40px' }}>{icon}</ListItemIcon>
          )}
          <ListItemText>
            <Typography fontWeight={400}>{text}</Typography>
          </ListItemText>
          <ChevronRightIcon fontSize="small" />
        </ListItemButton>
      </Box>
      <Popper
        open={open}
        anchorEl={anchorRef.current}
        placement="right-start"
        style={{ zIndex: theme.zIndex.modal }}
      >
        <Paper
          ref={popperRef}
          onMouseEnter={handlePopperMouseEnter}
          onMouseLeave={handlePopperMouseLeave}
          sx={{ py: 1 }}
        >
          {subItems.map((item) => {
            const selected = item.selected ?? router.asPath === item.url
            return (
              <MenuItem
                key={item.url ?? item.text}
                onClick={() => handleMenuItemClick(item)}
                selected={selected}
                sx={{
                  color: theme.palette.text.primary,
                  '&.Mui-selected': {
                    backgroundColor: `${baseColor}30`,
                  },
                  '&:hover': {
                    backgroundColor: `${baseColor}30`,
                  },
                }}
              >
                {item.text}
              </MenuItem>
            )
          })}
        </Paper>
      </Popper>
    </>
  )
}
