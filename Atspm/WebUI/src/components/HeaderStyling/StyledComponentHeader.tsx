import { Box, Typography } from '@mui/material'

interface styleComponentHeaderType {
  header: string
}
export const commonPaperStyle = {
  flexGrow: 1,
  display: 'flex',
  flexDirection: 'column',
  alignItems: 'center',
}

export const StyledComponentHeader = ({ header }: styleComponentHeaderType) => {
  return (
    <Box
      sx={{
        p: 1,
        pl: 2,
        mb: 1,
        backgroundColor: 'action.hover',
        width: '100%',
        textAlign: 'left',
      }}
    >
      <Typography variant="subtitle2">{header}</Typography>
    </Box>
  )
}
