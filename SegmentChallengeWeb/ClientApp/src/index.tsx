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

fetch('/config/env.json')
    .then(resp => {
        if (resp.status === 200) {
            const contentType = resp.headers.get('Content-Type');
            if (contentType && (contentType.startsWith('application/json') || contentType.startsWith('text/json'))) {
                return resp.json();
            } else {
                console.warn(`Unexpected content type for /config/env.json: ${contentType}`);
            }
        } else {
            console.warn(`Unexpected status code for /config/env.json: ${resp.status}`);
        }

        return {};
    })
    .catch(err => {
        console.warn('Error fetching config json: ' + (err.stack || err.message));
        return {};
    })
    .then((config: any) => {
        // Get the application-wide store instance, prepopulating with state from the server where available.
        const initialState = {
            login: { loggedInUser },
            config: {
                siteTitle: config.siteTitle ?? process.env.REACT_APP_SITE_TITLE ?? 'Strava Segment Challenge',
                siteLogo: config.siteLogo ?? process.env.REACT_APP_SITE_LOGO,
                siteFooter: config.siteFooter ?? process.env.REACT_APP_SITE_FOOTER,
                apiBaseUrl: config.apiBaseUrl ?? process.env.REACT_APP_API_BASE_URL ?? ''
            }
        };

        document.title = initialState.config.siteTitle;

        const store = configureStore(history, initialState);

        ReactDOM.render(
            <Provider store={store}>
                <ConnectedRouter history={history}>
                    <App/>
                </ConnectedRouter>
            </Provider>,
            document.getElementById('root')
        );

        registerServiceWorker();
    });
