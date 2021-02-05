import * as React from "react";
import {connect, Matching} from "react-redux";
import {UploadChallengeGpxState} from "../store/UploadChallengeGpx";
import * as ChallengeDetailStore from "../store/ChallengeDetails";
import * as UploadChallengeGpxStore from "../store/UploadChallengeGpx";
import {LoginState} from "../store/Login";
import {ApplicationState} from "../store";
import {ChangeEvent} from "react";

type UploadChallengeProps =
    UploadChallengeGpxStore.UploadChallengeGpxState &
    ChallengeDetailStore.ChallengeDetailsState &
    { login?: LoginState } &
    {
        onFileSelected: (event: ChangeEvent<HTMLInputElement>) => void,
        uploadFile: () => void,
        cancelUpload: () => void
    };

class UploadChallengeGpx extends React.PureComponent<Matching<UploadChallengeProps, UploadChallengeProps>, UploadChallengeGpxState> {
    constructor(props: UploadChallengeProps) {
        super(props);

        this.state = {};
    }

    public render() {
        let sectionBody;

        if (this.props.uploaded) {
            sectionBody = (
                <div>
                    <span>GPX data successfully uploaded.</span>
                </div>
            );
        } else if (this.props.uploading) {
            sectionBody = (
                <div>
                    <span>Uploading File: {this.props.selectedFile?.name || "unknown"}</span>
                </div>
            );
        } else if (this.props.errorMessage) {
            sectionBody = (
                <div>
                    <span>An error occurred: {this.props.errorMessage}</span>
                </div>
            );
        } else if (this.props.selectedFile) {
            sectionBody = (
                <div>
                    <span>File selected: {this.props.selectedFile?.name}</span>
                    <button onClick={() => this.props.cancelUpload()} className="cancel-button">Cancel</button>
                    <button onClick={() => this.props.uploadFile()} className="ok-button">Upload GPX</button>

                </div>
            );
        } else {
            sectionBody = (
                <div>
                    <input type="file" name="challengeGxp" onChange={(e) => this.props.onFileSelected(e)}/>
                </div>
            );
        }

        return (
            <div className="row">
                <h3>
                    Upload Segment GPX Data
                </h3>
                {sectionBody}
            </div>
        );
    }
}

export default connect(
    (state: ApplicationState) => ({...state.uploadChallengeGpx, ...state.challengeDetails, login: state.login}),
    UploadChallengeGpxStore.actionCreators
)(UploadChallengeGpx);
