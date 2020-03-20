import jwt from "jwt-simple";

export function hasContent(response: Response) {
    const contentLength = response.headers.get('Content-Length');

    return contentLength && Number(contentLength) > 0;
}

export function generateErrorMessage(resource: string, status: number, statusText: string, detail: string) {
    if (status >= 500 && status < 600) {
        return `The ${resource} is currently unavailable. Please try again later or contact the webmaster if the problem persists. Error detail: ${detail}`;
    } else {
        return `An error occurred fetching or setting the ${resource}. Please contact the webmaster. Error detail: ${detail}`;
    }
}

function getCookie(name: string): string | undefined {
    let cookies = decodeURIComponent(document.cookie);
    for (let cookie of cookies.split(';')) {
        cookie = cookie.trimLeft();
        if (cookie.indexOf(name) === 0) {
            return cookie.substring(name.length + 1, cookie.length);
        }
    }

    return undefined;
}

export function getLoggedInUser() {
    const id_token = getCookie('id_token');
    let loggedInUser = undefined;
    if (id_token) {
        loggedInUser = jwt.decode(id_token, '', true);
        console.log(loggedInUser);

        if (typeof loggedInUser.user_data === "string") {
            loggedInUser.user_data = JSON.parse(loggedInUser.user_data);
        }
    }
    return loggedInUser;
}
