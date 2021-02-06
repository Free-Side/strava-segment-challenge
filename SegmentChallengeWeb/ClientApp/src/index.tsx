import * as React from 'react';
import * as ReactDOM from 'react-dom';
import { Provider } from 'react-redux';
import { ConnectedRouter } from 'connected-react-router';
import { createBrowserHistory } from 'history';
import configureStore from './store/configureStore';
import App from './App';
import registerServiceWorker from './registerServiceWorker';
import * as RestHelper from './RestHelper';

// Create browser history to use in the Redux store
const baseUrl = document.getElementsByTagName('base')[0].getAttribute('href') as string;
const history = createBrowserHistory({ basename: baseUrl });

// Check user login state
let loggedInUser = RestHelper.getLoggedInUser();

// Get the application-wide store instance, prepopulating with state from the server where available.
const initialState = {
    login: {loggedInUser},
    config: {
        // Todo: make it so that this can be configured at runtime instead of during build
        siteTitle: process.env.REACT_APP_SITE_TITLE || 'Strava Segment Challenge',
        siteLogo: process.env.REACT_APP_SITE_LOGO,
        siteFooter: process.env.REACT_APP_SITE_FOOTER,
        apiBaseUrl: process.env.REACT_APP_API_BASE_URL || ''
    }
};
const store = configureStore(history, initialState);

ReactDOM.render(
  <Provider store={store}>
    <ConnectedRouter history={history}>
      <App />
    </ConnectedRouter>
  </Provider>,
  document.getElementById('root')
);

registerServiceWorker();
