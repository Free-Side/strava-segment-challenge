import * as React from 'react';
import {connect, Matching} from "react-redux";
import * as ChallengeDetailsStore from "../store/ChallengeDetails";
import {ApplicationState} from "../store";

type NoEffortListProps =
    ChallengeDetailsStore.ChallengeDetailsState;

class NoEffortList extends React.PureComponent<Matching<NoEffortListProps, NoEffortListProps>> {
    public render() {
        return (
            <React.Fragment>
                {this.props.errorMessage &&
                <span className="error-message">{this.props.errorMessage}</span>}
                {this.props.allAthletes && this.props.allEfforts ?
                    this.renderNoEffortListTable(this.props.allAthletes, this.props.allEfforts) :
                    NoEffortList.renderLoadingIndicator()}
            </React.Fragment>
        );
    }

    private renderNoEffortListTable(athletes: ChallengeDetailsStore.Athlete[], efforts: ChallengeDetailsStore.Effort[]) {
        const showCategory = !(this.props.selectedCategory.maximumAge && this.props.selectedCategory.gender);
        const athletesWithEffort = new Set<number>(efforts.map(e => e.athleteId));
        const athletesWithNoEfforts = athletes.filter(a => !athletesWithEffort.has(a.id));
        if (athletesWithNoEfforts.length > 0) {
            return (
                <React.Fragment>
                    <h3>Athletes With No Times</h3>
                    <table className='main-table table-striped'>
                        <thead>
                        <tr>
                            <td>Athlete</td>
                            {showCategory && <td>Category</td>}
                        </tr>
                        </thead>
                        <tbody>
                        {athletesWithNoEfforts.map((athlete: ChallengeDetailsStore.Athlete) =>
                            <tr key={athlete.id}>
                                <td>{athlete.displayName}</td>
                                {showCategory && <td>{this.getCategory(athlete.age, athlete.gender)}</td>}
                            </tr>
                        )}
                        </tbody>
                    </table>
                    <p><em>
                        Note: If you recently uploaded a ride it make take 30 minutes to an hour for your segment times to
                        appear here. If it takes longer please contact support.
                    </em></p>
                </React.Fragment>
            );
        }
    }

    private getCategory(athleteAge: number, athleteGender: string): string {
        if (this.props.ageGroups) {
            let gender = 'Other';
            switch (athleteGender) {
                case 'm':
                case 'M':
                    gender = 'Men';
                    break;
                case 'f':
                case 'F':
                    gender = 'Women';
                    break;
            }

            const ageGroup = this.props.ageGroups.filter(a => a.maximumAge >= athleteAge)[0];
            if (ageGroup) {
                return `${gender}, ${ageGroup.description}`;
            } else {
                return gender;
            }
        } else {
            return 'Unknown';
        }
    }

    private static renderLoadingIndicator() {
        return (
            <div className="loading-indicator">Loading? ...</div>
        )
    }
}

export default connect(
    (state: ApplicationState) => state.challengeDetails
)(NoEffortList);
