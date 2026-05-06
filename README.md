# Riehn Momentum Radar

Riehn Momentum Radar is a local Windows application that displays the strongest momentum stocks from the S&P 500. The ranking is based on the percentage price change over the last 180 trading days.

The app is designed to be simple to use: start the executable, let the data load, and view the Top 10 ranking.

## Getting Started

The executable file is included in this folder:

```text
Riehn Momentum Radar.exe
```

Double-click the file to start the app.

No installation, browser, or local server is required. The app only needs an active internet connection.

On first launch, Windows SmartScreen may show a warning because the executable is not code-signed. This does not automatically mean the app is unsafe; it means Windows does not recognize the publisher yet.

## What The App Shows

- Top 5 momentum stocks, visually highlighted
- Places 6 to 10 in a table
- Ticker, company name, and rank
- Performance over 180 trading days
- Start price at the 180-trading-day reference date
- Latest available price, including pre-market and post-market quotes when available
- Calculation period

Example:

```text
Start: $44.54 | Today: $1406.32
08/15/2025 to 05/05/2026
```

## Data Sources

The app does not use a fixed built-in stock list. On each refresh, it downloads the current S&P 500 constituent list from online sources.

Sources used:

- DataHub for the S&P 500 constituent list
- Wikipedia as a fallback for the S&P 500 constituent list
- Yahoo Finance for daily and intraday stock price data

The historical start price is based on daily prices. The latest price uses current intraday data when available, including pre-market and post-market quotes from Yahoo Finance.

## Calculation

The app ranks all calculable S&P 500 stocks by momentum:

```text
(latest price - price 180 trading days ago) / price 180 trading days ago * 100
```

Important: The app uses 180 trading days, not 180 calendar days.

If a stock does not have enough price history for 180 trading days, it is skipped for that calculation.

## Refresh Schedule

The app refreshes:

- automatically on startup
- manually when the Refresh button is clicked
- during the U.S. pre-market session
- shortly after the U.S. market open
- 4 hours after the U.S. market open
- at the U.S. market close

The schedule is calculated internally in Berlin time.

## Notifications

Notifications are enabled by default.

A notification is shown only when the Top 5 changes, for example when a new stock enters the Top 5 or when the order of the Top 5 changes.

Notifications can be disabled in the app using the Notifications checkbox.

## Languages

The app supports:

- German
- English
- Spanish

The selected language is stored locally on the user's device.

## Local Data

The app stores a small amount of data locally on the user's device:

- selected language
- notification preference
- latest successful data snapshot
- previous Top 5 list for change detection

This local data is not sent to the developer.

## Security

The app does not open a local server or network port. It does not execute external data as code.

Network access is limited to outgoing HTTPS GET requests to allowlisted hosts for stock constituent data and price data. Redirects are checked and must also point to allowed HTTPS sources.

Response sizes are limited to protect the app from unexpectedly large or malformed responses.

## Disclaimer

Riehn Momentum Radar is an informational monitoring tool. It is not financial advice, investment advice, or a recommendation to buy or sell securities.
