import * as React from "react";
import { connect, Matching } from "react-redux";
import { UploadEffortGpxState } from "../store/UploadEffortGpx";
import * as ChallengeDetailStore from "../store/ChallengeDetails";
import * as UploadEffortGpxStore from "../store/UploadEffortGpx";
import { LoginInfo, LoginState } from "../store/Login";
import { ApplicationState } from "../store";
import { ChangeEvent } from "react";

type UploadEffortProps =
    UploadEffortGpxStore.UploadEffortGpxState &
    ChallengeDetailStore.ChallengeDetailsState &
    { loggedInUser?: LoginInfo } &
    {
        onAthleteIdChanged: (event: ChangeEvent<HTMLInputElement>) => void,
        onFileSelected: (event: ChangeEvent<HTMLInputElement>) => void,
        uploadFile: () => void,
        cancelUpload: () => void
    };

class UploadEffortGpx extends React.PureComponent<Matching<UploadEffortProps, UploadEffortProps>, UploadEffortGpxState> {
    constructor(props: UploadEffortProps) {
        super(props);

        this.state = {
            athleteId: props.athleteId
        };
    }

    public render() {
        let fileInputRow;

        if (this.props.uploaded) {
            fileInputRow = (
                <div>
                    <span>GPX data successfully uploaded.</span>
                </div>
            );
        } else if (this.props.uploading) {
            fileInputRow = (
                <div>
                    <span>Uploading File: {this.props.selectedFile?.name || "unknown"}</span>
                </div>
            );
        } else if (this.props.errorMessage) {
            fileInputRow = (
                <div>
                    <span>An error occurred: {this.props.errorMessage}</span>
                </div>
            );
        } else if (this.props.selectedFile) {
            fileInputRow = (
                <div>
                    <span>File selected: {this.props.selectedFile?.name}</span>
                    <button onClick={() => this.props.cancelUpload()} className="cancel-button">Cancel</button>
                    <button onClick={() => this.props.uploadFile()} className="ok-button">Upload GPX</button>

                </div>
            );
        } else {
            fileInputRow = (
                <div>
                    <input type="file" name="challengeGxp" onChange={(e) => this.props.onFileSelected(e)} />
                </div>
            );
        }

        return (
            <div className="row">
                <h3>
                    Upload Effort GPX Data
                </h3>
                {this.props.loggedInUser && this.props.loggedInUser.user_data.is_admin &&
                    <div>
                        <label>Athlete:
                            <input type="number" value={this.state.athleteId} onChange={(e) => this.props.onAthleteIdChanged(e)} />
                        </label>
                    </div>}
                {fileInputRow}
            </div>
        );
    }
}

export default connect(
    (state: ApplicationState) => ({ ...state.uploadEffortGpx, ...state.challengeDetails, loggedInUser: state.login?.loggedInUser }),
    UploadEffortGpxStore.actionCreators
)(UploadEffortGpx);
